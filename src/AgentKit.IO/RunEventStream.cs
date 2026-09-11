// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Projects one internal event subscription and independent final-result task onto the canonical stream contract.</summary>
/// <typeparam name="TOutput">The validated immutable output type.</typeparam>
/// <remarks>The producer owns settlement and task completion. This adapter owns only its subscription, validates final correlation, and never cancels or completes the run.</remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "This adapter implements the canonical typed event stream contract.")]
internal sealed class RunEventStream<TOutput>: IAgentRunStream<TOutput>
{
    private readonly RunEventSubscription _subscription;
    private readonly Func<RunEventHubOperation, RunEventHubObservation> _observe;

    /// <summary>Captures validated stream coordinates and immediately starts independent completion observation.</summary>
    /// <param name="subscription">The nonnull already registered event subscription owned by this adapter.</param>
    /// <param name="agentId">The nondefault accepted agent.</param>
    /// <param name="sessionId">The nondefault owning session.</param>
    /// <param name="conversationId">The optional nondefault conversation.</param>
    /// <param name="runId">The nondefault accepted run.</param>
    /// <param name="completion">The nonnull producer-owned final-result task.</param>
    /// <param name="observe">The nonnull factory for isolated run-correlated observation.</param>
    /// <exception cref="ArgumentNullException">A subscription, task or observation factory is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A required or present optional identity is default.</exception>
    internal RunEventStream(RunEventSubscription subscription, AgentId agentId, SessionId sessionId, ConversationId? conversationId,
        RunId runId, Task<AgentRunFinished<TOutput>> completion, Func<RunEventHubOperation, RunEventHubObservation> observe)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        if (conversationId is { } conversation) { ArgumentOutOfRangeException.ThrowIfEqual(conversation, default, nameof(conversationId)); }
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(observe);
        _subscription = subscription; _observe = observe;
        AgentId = agentId; SessionId = sessionId; ConversationId = conversationId; RunId = runId;
        Completion = ValidateCompletionAsync(completion);
    }
    /// <inheritdoc/>
    public AgentId AgentId { get; }
    /// <inheritdoc/>
    public SessionId SessionId { get; }
    /// <inheritdoc/>
    public ConversationId? ConversationId { get; }
    /// <inheritdoc/>
    public RunId RunId { get; }
    /// <inheritdoc/>
    public Task<AgentRunFinished<TOutput>> Completion { get; }
    /// <inheritdoc/>
    public IAsyncEnumerable<RunEvent> ReadAllAsync(CancellationToken cancellationToken = default) => _subscription.ReadAllAsync(cancellationToken);
    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _subscription.DisposeAsync();

    /// <summary>Validates the one completed envelope before exposing it, preserving producer failure and caller activity parentage.</summary>
    /// <param name="completion">The constructor-validated producer task.</param>
    /// <returns>The exact matching producer envelope, or a faulted task if its correlation is invalid.</returns>
    private async Task<AgentRunFinished<TOutput>> ValidateCompletionAsync(Task<AgentRunFinished<TOutput>> completion)
    {
        Debug.Assert(completion is not null, "The constructor validates the producer completion task before capturing it.");
        using var observation = _observe(RunEventHubOperation.AwaitCompletion);
        try
        {
            var result = await completion.ConfigureAwait(false);
            if (result is null || result.AgentId != AgentId || result.SessionId != SessionId
                || result.ConversationId != ConversationId || result.RunId != RunId)
            {
                throw new InvalidOperationException("The producer returned a final result for a different run or no result.");
            }
            observation.Finish(RunEventHubOutcome.Succeeded);
            return result;
        }
        catch (OperationCanceledException)
        {
            observation.Finish(RunEventHubOutcome.Cancelled);
            throw;
        }
    }
}
