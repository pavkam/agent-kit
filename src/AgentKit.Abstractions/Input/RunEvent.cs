// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Represents one extensible, correlated event emitted while an accepted run
/// is active.
/// </summary>
/// <remarks>
/// The stable protocol identity is <c>(RunId, Sequence)</c>. Sequence is
/// allocated by the run's publisher and is monotonic only within that run;
/// this value validates its local shape but neither allocates a sequence nor
/// verifies cross-event ordering. The event timestamp is captured evidence,
/// not an ambient-clock reading that this constructor can validate. Third
/// parties may define additional event variants using this protected
/// validating constructor.
/// </remarks>
public abstract record RunEvent
{
    /// <summary>
    /// Initializes an event with its complete run correlation and publication
    /// durability.
    /// </summary>
    /// <param name="agentId">The nondefault agent that owns the run.</param>
    /// <param name="sessionId">The nondefault session containing the run.</param>
    /// <param name="conversationId">
    /// The optional nondefault conversation correlated with the event.
    /// </param>
    /// <param name="runId">The nondefault run that owns the event sequence.</param>
    /// <param name="turnId">
    /// The optional nondefault turn producing the event.
    /// </param>
    /// <param name="sequence">The positive sequence unique within <paramref name="runId"/>.</param>
    /// <param name="occurredAt">The timestamp retained as event evidence.</param>
    /// <param name="durability">Whether the event is live or durable.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A required identity is default, an optional supplied identity is
    /// default, <paramref name="sequence"/> is not positive, or
    /// <paramref name="durability"/> is undefined.
    /// </exception>
    protected RunEvent(
        AgentId agentId,
        SessionId sessionId,
        ConversationId? conversationId,
        RunId runId,
        TurnId? turnId,
        long sequence,
        DateTimeOffset occurredAt,
        RunEventDurability durability)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default, nameof(sessionId));
        if (conversationId is { } capturedConversationId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(
                capturedConversationId,
                default,
                nameof(conversationId));
        }

        ArgumentOutOfRangeException.ThrowIfEqual(runId, default, nameof(runId));
        if (turnId is { } capturedTurnId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(capturedTurnId, default, nameof(turnId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        ArgumentOutOfRangeException.ThrowIfUndefined(durability);

        AgentId = agentId;
        SessionId = sessionId;
        ConversationId = conversationId;
        RunId = runId;
        TurnId = turnId;
        Sequence = sequence;
        OccurredAt = occurredAt;
        Durability = durability;
    }

    /// <summary>Gets the agent that owns the event's run.</summary>
    /// <value>The nondefault agent identity captured at event creation.</value>
    public AgentId AgentId { get; }

    /// <summary>Gets the session containing the event's run.</summary>
    /// <value>The nondefault session identity captured at event creation.</value>
    public SessionId SessionId { get; }

    /// <summary>Gets the optional conversation correlated with the event.</summary>
    /// <value>
    /// A nondefault conversation identity when one is correlated with this
    /// event; otherwise <see langword="null"/>.
    /// </value>
    public ConversationId? ConversationId { get; }

    /// <summary>Gets the run that owns this event sequence.</summary>
    /// <value>The nondefault run identity in the event's protocol key.</value>
    public RunId RunId { get; }

    /// <summary>Gets the optional turn that produced this event.</summary>
    /// <value>
    /// A nondefault turn identity when the event is attributable to one;
    /// otherwise <see langword="null"/>.
    /// </value>
    public TurnId? TurnId { get; }

    /// <summary>Gets the event's positive sequence within its run.</summary>
    /// <value>The publisher-assigned sequence in the composite run protocol key.</value>
    public long Sequence { get; }

    /// <summary>Gets the timestamp retained as event evidence.</summary>
    /// <value>The supplied instant, without ambient-clock reinterpretation.</value>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Gets whether this event is provisional live or durable.</summary>
    /// <value>The defined publication durability selected by the event variant.</value>
    public RunEventDurability Durability { get; }
}
