// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Publishes durable evidence that one message was committed to a session
/// branch.
/// </summary>
/// <remarks>
/// The event retains the committed message and resulting branch version; it
/// does not expose or reconstruct the message content.
/// </remarks>
public sealed record MessageCommittedEvent: RunEvent
{
    /// <summary>
    /// Initializes a durable committed-message event with its exact session
    /// version evidence.
    /// </summary>
    /// <param name="agentId">The nondefault agent that owns the run.</param>
    /// <param name="sessionId">The nondefault session containing the run.</param>
    /// <param name="conversationId">The optional nondefault conversation correlated with the event.</param>
    /// <param name="runId">The nondefault run that owns the event sequence.</param>
    /// <param name="turnId">The required nonnull, nondefault turn that committed the message.</param>
    /// <param name="sequence">The positive sequence unique within the run.</param>
    /// <param name="occurredAt">The timestamp retained as event evidence.</param>
    /// <param name="messageId">The nondefault identity of the committed message.</param>
    /// <param name="sessionVersion">The positive branch version resulting from the commit.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A base correlation is invalid, <paramref name="turnId"/> is default,
    /// <paramref name="messageId"/> is default, or
    /// <paramref name="sessionVersion"/> is not positive.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="turnId"/> is null.</exception>
    public MessageCommittedEvent(
        AgentId agentId,
        SessionId sessionId,
        ConversationId? conversationId,
        RunId runId,
        TurnId? turnId,
        long sequence,
        DateTimeOffset occurredAt,
        MessageId messageId,
        SessionVersion sessionVersion)
        : base(
            agentId,
            sessionId,
            conversationId,
            runId,
            RequiredTurnId(turnId),
            sequence,
            occurredAt,
            RunEventDurability.Durable)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(messageId, default, nameof(messageId));
        ArgumentOutOfRangeException.ThrowIfLessThan(sessionVersion.Value, 1, nameof(sessionVersion));

        MessageId = messageId;
        SessionVersion = sessionVersion;
    }

    /// <summary>Gets the identity of the committed message.</summary>
    /// <value>The nondefault durable message identity.</value>
    public MessageId MessageId { get; }

    /// <summary>Gets the positive branch version resulting from the commit.</summary>
    /// <value>The exact post-commit session version retained as durable evidence.</value>
    public SessionVersion SessionVersion { get; }

    /// <summary>
    /// Validates a required nullable turn argument before the base event is
    /// initialized.
    /// </summary>
    /// <param name="turnId">The required nonnull, nondefault turn identity.</param>
    /// <returns>The validated turn identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="turnId"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="turnId"/> is default.</exception>
    private static TurnId RequiredTurnId(TurnId? turnId)
    {
        // Preserve the BCL's documented ArgumentNullException and parameter
        // name at this value-construction boundary; this is not a hot loop.
#pragma warning disable CA1871 // Nullable TurnId is intentionally validated by the BCL null guard.
        ArgumentNullException.ThrowIfNull(turnId, nameof(turnId));
#pragma warning restore CA1871
        var requiredTurnId = turnId.GetValueOrDefault();
        ArgumentOutOfRangeException.ThrowIfEqual(requiredTurnId, default, nameof(turnId));
        return requiredTurnId;
    }
}
