// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The first-party <see cref="ITaskDelegationChannel"/>: runs a delegated task as one turn of the target agent on
/// the same engine, in a new session owned by the delegating identity, and returns a bounded summary.
/// </summary>
/// <remarks>
/// <para>
/// The channel is what the delegation broker hands an authorized prompt to. It resolves the target through
/// <see cref="AgentEngine.GetAgentAsync"/>, so only definitions the engine hosts can be delegated to; an unknown
/// target is a typed rejection. The child turn runs under the parent's identity, the prompt's deadline, and the
/// narrower of the prompt's turn budget and the target definition's own limit. Only the child's assistant text,
/// bounded by <see cref="EngineDelegationChannelOptions.MaximumSummaryCharacters"/>, flows back.
/// </para>
/// <para>
/// The engine is resolved lazily on first use rather than injected: the engine hosts the tool that hosts the broker
/// that hosts this channel, so eager injection would be a constructor cycle. The channel therefore never runs
/// before the engine exists, and it holds no state between delegations.
/// </para>
/// </remarks>
public sealed class EngineDelegationChannel: ITaskDelegationChannel
{
    private readonly IServiceProvider _services;
    private readonly TimeProvider _timeProvider;
    private readonly IIdentifierGenerator<GoalId> _goalIds;
    private readonly EngineDelegationChannelOptions _options;
    private readonly ILogger<EngineDelegationChannel> _logger;
    private AgentEngine? _engine;

    /// <summary>Initializes the channel.</summary>
    /// <param name="services">The root provider the hosting engine is resolved from on first use.</param>
    /// <param name="timeProvider">The clock the prompt's deadline is measured against.</param>
    /// <param name="goalIds">Allocates the child goal identity recorded on the result.</param>
    /// <param name="options">The channel's bounds.</param>
    /// <param name="logger">Optional content-safe diagnostics.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="EngineDelegationChannelOptions.MaximumSummaryCharacters"/> is not positive.</exception>
    public EngineDelegationChannel(
        IServiceProvider services,
        TimeProvider timeProvider,
        IIdentifierGenerator<GoalId> goalIds,
        IOptions<EngineDelegationChannelOptions> options,
        ILogger<EngineDelegationChannel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(goalIds);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Value, nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumSummaryCharacters, nameof(options));
        _services = services;
        _timeProvider = timeProvider;
        _goalIds = goalIds;
        _options = options.Value;
        _logger = logger ?? NullLogger<EngineDelegationChannel>.Instance;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see cref="TaskDelegationRejected"/> when the target is not hosted, the deadline has already passed or
    /// elapses before the child turn completes, the child's session could not be admitted, or the target's session
    /// lane rejected the turn. Cancellation of <paramref name="cancellationToken"/> propagates. A child run that
    /// settles with a typed cancelled outcome is reported as <see cref="TaskDelegationStatus.Cancelled"/> with its
    /// real session and run identities.
    /// </remarks>
    public async ValueTask<TaskDelegationResult> DelegateAsync(TaskDelegationPrompt prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        cancellationToken.ThrowIfCancellationRequested();

        var remaining = prompt.Deadline - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            return new TaskDelegationRejected(prompt.Id, "The delegation deadline had already passed.");
        }

        var engine = _engine ??= _services.GetRequiredService<AgentEngine>();
        var resolution = await engine.GetAgentAsync(prompt.TargetAgentId, cancellationToken).ConfigureAwait(false);
        if (resolution is not ResolvedAgent { Agent: var target })
        {
            EngineDelegationLog.TargetUnknown(_logger, prompt.Id, prompt.TargetAgentId);
            return new TaskDelegationRejected(prompt.Id, "The target agent is not hosted by this engine.");
        }

        using var deadline = new CancellationTokenSource(remaining, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var maxTurns = Math.Min(prompt.Budget.MaximumTurns, target.Definition.RunDefaults.MaxTurns);
        AgentLoopResult result;
        try
        {
            result = await target.SendAsync(
                new AgentSendRequest(prompt.Identity, Objective(prompt), sessionId: null, maxTurns: maxTurns),
                linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            EngineDelegationLog.DeadlineElapsed(_logger, prompt.Id, prompt.TargetAgentId);
            return new TaskDelegationRejected(prompt.Id, "The delegation deadline elapsed before the child turn completed.");
        }
        catch (AgentAdmissionRejectedException exception)
        {
            EngineDelegationLog.ChildRejected(_logger, prompt.Id, prompt.TargetAgentId, exception.GetType().Name);
            return new TaskDelegationRejected(prompt.Id, exception.Rejection.Reason);
        }

        var status = result.Outcome switch
        {
            RunSucceeded => TaskDelegationStatus.Succeeded,
            RunCancelled => TaskDelegationStatus.Cancelled,
            RunPolicyHalted => TaskDelegationStatus.Blocked,
            _ => TaskDelegationStatus.Failed,
        };
        EngineDelegationLog.ChildSettled(_logger, prompt.Id, prompt.TargetAgentId, result.SessionId, result.RunId, status);
        return new TaskDelegationChildResult(
            prompt.Id,
            _goalIds.Create(),
            prompt.TargetAgentId,
            result.SessionId,
            childAttemptId: null,
            result.RunId,
            status,
            Summary(result, _options.MaximumSummaryCharacters),
            SideEffects(result));
    }

    private static string Objective(TaskDelegationPrompt prompt)
    {
        Debug.Assert(prompt is not null, "The public boundary validates the prompt.");
        Debug.Assert(!prompt.AcceptanceCriteria.IsEmpty, "The prompt contract requires at least one acceptance criterion.");
        return $"{prompt.Objective}\n\nAcceptance criteria:\n- {string.Join("\n- ", prompt.AcceptanceCriteria)}";
    }

    private static string Summary(AgentLoopResult result, int maximumCharacters)
    {
        Debug.Assert(maximumCharacters > 0, "The constructor validates the summary bound.");
        var text = string.Concat(result.NewMessages
            .OfType<AssistantMessage>()
            .SelectMany(static message => message.Parts.OfType<TextPart>())
            .Select(static part => part.Text));
        return text.Length switch
        {
            0 => $"The child run settled as {result.Outcome.GetType().Name} without a final answer.",
            var length when length <= maximumCharacters => text,
            _ => text[..maximumCharacters],
        };
    }

    private static SideEffectCertainty SideEffects(AgentLoopResult result)
    {
        var outcomes = result.NewMessages
            .OfType<ToolMessage>()
            .SelectMany(static message => message.Parts.OfType<ToolResultPart>())
            .Select(static part => part.Outcome.SideEffectCertainty)
            .ToList();
        return outcomes.Count == 0 || outcomes.TrueForAll(static certainty => certainty == SideEffectCertainty.DefinitelyNotPerformed)
            ? SideEffectCertainty.DefinitelyNotPerformed
            : outcomes.TrueForAll(static certainty => certainty is SideEffectCertainty.DefinitelyPerformed or SideEffectCertainty.DefinitelyNotPerformed)
                ? SideEffectCertainty.DefinitelyPerformed
                : SideEffectCertainty.Unknown;
    }
}
