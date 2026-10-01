// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting;

using System.Security.Cryptography;
using System.Text;

/// <summary>Is the first-party <see cref="IDelegationChildRunner"/>: it runs a claimed child attempt as one turn of the target agent on the hosting engine.</summary>
/// <remarks>
/// <para>
/// The runner resolves the target through <see cref="AgentEngine.GetAgentAsync"/>, so only definitions the engine hosts can be
/// delegated to. It provisions a session owned by the delegating identity, and runs the child under that identity, the
/// delegation deadline, and the narrower of the delegation's turn ceiling and the target definition's own limit. The run is
/// awaited through <see cref="Agent.RunAsync{TOutput}(SessionId, ExecutionIdentity, AgentInput, ConversationId?, AgentRunOptions?, ExecutionLaneId?, CancellationToken)"/>,
/// which works with every composed output publisher, and cancelling the token cancels the loop so the run settles as cancelled or the
/// wait throws. Only the child's assistant text, bounded by
/// <see cref="GoalWorkerOptions.MaximumSummaryCharacters"/>, flows back.
/// </para>
/// <para>
/// The engine is resolved lazily on first use rather than injected: the engine hosts the tool that hosts the delegation
/// coordinator that this runner serves, so eager injection would be a constructor cycle. The runner holds no state between
/// attempts and is thread-safe.
/// </para>
/// </remarks>
public sealed class EngineDelegationChildRunner: IDelegationChildRunner
{
    private readonly IServiceProvider _services;
    private readonly GoalWorkerOptions _options;
    private readonly ILogger<EngineDelegationChildRunner> _logger;
    private AgentEngine? _engine;

    /// <summary>Initializes the runner.</summary>
    /// <param name="services">The root provider the hosting engine is resolved from on first use.</param>
    /// <param name="options">The worker options carrying the summary bound.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="options"/> is null.</exception>
    public EngineDelegationChildRunner(IServiceProvider services, IOptions<GoalWorkerOptions> options, ILogger<EngineDelegationChildRunner>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        _services = services;
        _options = options.Value;
        _logger = logger ?? NullLogger<EngineDelegationChildRunner>.Instance;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The hosting engine has been disposed.</exception>
    public async ValueTask<SessionId?> ProvisionSessionAsync(DelegationRequest delegation, GoalId childGoalId, int attemptNumber, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delegation);
        ArgumentOutOfRangeException.ThrowIfEqual(childGoalId, default);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attemptNumber);
        var engine = _engine ??= _services.GetRequiredService<AgentEngine>();
        var resolution = await engine.GetAgentAsync(delegation.TargetAgentId, cancellationToken).ConfigureAwait(false);
        if (resolution is not ResolvedAgent { Agent: var target })
        {
            WorkerObservation.Safe(() => GoalWorkerLog.TargetUnknown(_logger, delegation.Id, delegation.TargetAgentId));
            return null;
        }

        var created = await target.CreateSessionAsync(
            delegation.Authorization.Identity,
            new IdempotencyKey($"agentkit.goals.child-session:{childGoalId}:{attemptNumber}"),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return created is AgentSessionCreated session ? session.SessionId : null;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The hosting engine has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the run settles.</exception>
    public async ValueTask<DelegationChildRunResult> RunAsync(DelegationChildRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var delegation = request.Delegation;
        var engine = _engine ??= _services.GetRequiredService<AgentEngine>();
        var resolution = await engine.GetAgentAsync(delegation.TargetAgentId, cancellationToken).ConfigureAwait(false);
        if (resolution is not ResolvedAgent { Agent: var target })
        {
            WorkerObservation.Safe(() => GoalWorkerLog.TargetUnknown(_logger, delegation.Id, delegation.TargetAgentId));
            return NotRun();
        }

        var maxTurns = Math.Min(delegation.Budget.Budget.MaximumTurns, target.Definition.RunDefaults.MaxTurns);
        var input = new AgentInput(
            ChildInputId(request.ChildGoalId, request.AttemptId),
            InputDelivery.Steer,
            [new TextPart(Objective(delegation), TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var run = await target.RunAsync<string>(
            request.SessionId,
            delegation.Authorization.Identity,
            input,
            options: new AgentRunOptions(maxTurns),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (run is not AgentRunFinished<string> finished)
        {
            var rejection = ((AgentRunRejected<string>) run).Failure;
            WorkerObservation.Safe(() => GoalWorkerLog.RunRejected(_logger, delegation.Id, rejection.Code.ToString()));
            return NotRun();
        }

        var status = finished.Outcome switch
        {
            RunSucceeded => DelegationStatus.Succeeded,
            RunCancelled => DelegationStatus.Cancelled,
            RunPolicyHalted => DelegationStatus.Blocked,
            _ => DelegationStatus.Failed,
        };
        return new DelegationChildRunResult(
            finished.RunId,
            status,
            status == DelegationStatus.Succeeded ? Summary(finished.NewMessages, finished.Outcome, _options.MaximumSummaryCharacters) : null,
            Usage(finished.NewMessages),
            SideEffects(finished.NewMessages));
    }

    private static DelegationChildRunResult NotRun() =>
        new(runId: null, DelegationStatus.Failed, summary: null, GoalBudgetUsage.None, SideEffectCertainty.DefinitelyNotPerformed);

    private static InputId ChildInputId(GoalId childGoalId, GoalAttemptId attemptId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"agentkit.goals.child-input:{childGoalId}:{attemptId}"));
        return new InputId(new Guid(hash.AsSpan(0, 16)));
    }

    private static string Objective(DelegationRequest delegation)
    {
        var builder = new StringBuilder(delegation.ChildGoal.Objective);
        if (!delegation.AcceptanceCriteria.Criteria.IsEmpty)
        {
            _ = builder.Append("\n\nAcceptance criteria:");
            foreach (var criterion in delegation.AcceptanceCriteria.Criteria)
            {
                _ = builder.Append("\n- ").Append(criterion);
            }
        }

        return builder.ToString();
    }

    private static string Summary(ImmutableArray<AgentMessage> messages, AgentRunOutcome outcome, int maximumCharacters)
    {
        Debug.Assert(maximumCharacters > 0, "The worker options validate the summary bound.");
        var text = string.Concat(messages
            .OfType<AssistantMessage>()
            .SelectMany(static message => message.Parts.OfType<TextPart>())
            .Select(static part => part.Text));
        return text.Length switch
        {
            0 => $"The child run settled as {outcome.GetType().Name} without a final answer.",
            var length when length <= maximumCharacters => text,
            _ => text[..maximumCharacters],
        };
    }

    private static GoalBudgetUsage Usage(ImmutableArray<AgentMessage> messages) => new(
        messages.OfType<AssistantMessage>().Count(),
        messages.OfType<ToolMessage>().SelectMany(static message => message.Parts.OfType<ToolResultPart>()).Count(),
        children: 0);

    private static SideEffectCertainty SideEffects(ImmutableArray<AgentMessage> messages)
    {
        var outcomes = messages
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
