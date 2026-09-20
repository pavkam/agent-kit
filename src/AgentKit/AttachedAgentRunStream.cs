// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Runtime.CompilerServices;

/// <summary>Replays durable run history from the session store, then tails a live publisher subscription.</summary>
/// <typeparam name="TOutput">The validated output snapshot type.</typeparam>
/// <remarks>
/// Attachment does not restart or drive the run. It validates the run is still active in the process-local registry,
/// synthesizes durable <see cref="MessageCommittedEvent"/> records from committed session entries, and forwards
/// subsequent live events from the run's existing publisher subscription.
/// </remarks>
internal sealed class AttachedAgentRunStream<TOutput>: IAgentRunStream<TOutput>
{
    private readonly IAgentRunStream<TOutput> _live;
    private readonly ActiveRunRegistration _registration;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes an attach stream over one active registration and its live tail.</summary>
    /// <param name="live">The publisher subscription receiving subsequent live events.</param>
    /// <param name="registration">The active-run evidence used for replay reads.</param>
    /// <param name="timeProvider">The engine clock used to stamp synthesized replay events.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    internal AttachedAgentRunStream(
        IAgentRunStream<TOutput> live,
        ActiveRunRegistration registration,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _live = live;
        _registration = registration;
        _timeProvider = timeProvider;
        AgentId = registration.AgentId;
        SessionId = registration.SessionId;
        ConversationId = registration.ConversationId;
        RunId = registration.RunId;
        Completion = live.Completion;
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
    public async IAsyncEnumerable<RunEvent> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sequence = 0L;
        await foreach (var replay in ReplayCommittedMessagesAsync(cancellationToken).ConfigureAwait(false))
        {
            sequence++;
            yield return new MessageCommittedEvent(
                replay.AgentId,
                replay.SessionId,
                replay.ConversationId,
                replay.RunId,
                replay.TurnId,
                sequence,
                replay.OccurredAt,
                replay.MessageId,
                replay.SessionVersion);
        }

        await foreach (var live in _live.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return live;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _live.DisposeAsync();

    private async IAsyncEnumerable<MessageCommittedEvent> ReplayCommittedMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var fromSequence = _registration.PreviousCursor.Sequence;
        var hasMore = true;
        while (hasMore)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await _registration.Sessions.ReadAsync(
                new SessionReadRequest(
                    _registration.OperationContext,
                    _registration.BranchId,
                    fromSequence,
                    pageSize: 32),
                _registration.SessionProfile,
                cancellationToken).ConfigureAwait(false);
            if (page is not SessionPage { Entries.Length: > 0 } loaded)
            {
                yield break;
            }

            foreach (var entry in loaded.Entries)
            {
                if (entry is not MessageSessionEntry messageEntry
                    || messageEntry.Message.RunId != _registration.RunId)
                {
                    continue;
                }

                if (messageEntry.Correlation is not InRunOperationCorrelation { TurnId: { } turnId })
                {
                    continue;
                }

                yield return new MessageCommittedEvent(
                    _registration.AgentId,
                    _registration.SessionId,
                    _registration.ConversationId,
                    _registration.RunId,
                    turnId,
                    sequence: 1,
                    _timeProvider.GetUtcNow(),
                    messageEntry.Message.Id,
                    loaded.Snapshot!.Version);
            }

            fromSequence = loaded.ThroughSequence;
            hasMore = loaded.HasMore;
        }
    }
}
