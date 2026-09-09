// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Publishes one incremental content fragment for a part of a model response.
/// </summary>
/// <remarks>
/// This is live event evidence. It is not durable session content and does
/// not claim that the corresponding response part or message was completed.
/// </remarks>
public sealed record ContentDeltaEvent: RunEvent
{
    /// <summary>
    /// Initializes a live content-delta event with complete request and part
    /// correlation.
    /// </summary>
    /// <param name="agentId">The nondefault agent that owns the run.</param>
    /// <param name="sessionId">The nondefault session containing the run.</param>
    /// <param name="conversationId">The optional nondefault conversation correlated with the event.</param>
    /// <param name="runId">The nondefault run that owns the event sequence.</param>
    /// <param name="turnId">The required nonnull, nondefault turn that produced the fragment.</param>
    /// <param name="sequence">The positive sequence unique within the run.</param>
    /// <param name="occurredAt">The timestamp retained as event evidence.</param>
    /// <param name="requestId">The nondefault model request receiving the fragment.</param>
    /// <param name="partIndex">The zero-based response-part index receiving the fragment.</param>
    /// <param name="delta">The nonnull incremental content fragment.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A base correlation is invalid, <paramref name="turnId"/> is default,
    /// <paramref name="requestId"/> is default, or
    /// <paramref name="partIndex"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="turnId"/> or <paramref name="delta"/> is null.
    /// </exception>
    public ContentDeltaEvent(
        AgentId agentId,
        SessionId sessionId,
        ConversationId? conversationId,
        RunId runId,
        TurnId? turnId,
        long sequence,
        DateTimeOffset occurredAt,
        ModelRequestId requestId,
        int partIndex,
        ContentDelta delta)
        : base(
            agentId,
            sessionId,
            conversationId,
            runId,
            RequiredTurnId(turnId),
            sequence,
            occurredAt,
            RunEventDurability.Live)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(requestId, default, nameof(requestId));
        ArgumentOutOfRangeException.ThrowIfNegative(partIndex);
        ArgumentNullException.ThrowIfNull(delta);

        RequestId = requestId;
        PartIndex = partIndex;
        Delta = delta;
    }

    /// <summary>Gets the model request receiving the fragment.</summary>
    /// <value>The nondefault request identity retained for model correlation.</value>
    public ModelRequestId RequestId { get; }

    /// <summary>Gets the zero-based index of the response part receiving the fragment.</summary>
    /// <value>A nonnegative index within the response represented by <see cref="RequestId"/>.</value>
    public int PartIndex { get; }

    /// <summary>Gets the incremental content fragment.</summary>
    /// <value>The nonnull immutable fragment exactly supplied by the model stream.</value>
    public ContentDelta Delta { get; }

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
