// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one message one agent sends to another agent's session.</summary>
/// <remarks>
/// A message is admitted input, not a prompt string handed around: it carries the sender, the recipient, the causal goal and
/// attempt when there is one, and an idempotency key, and it is admitted through the recipient's normal input path so it is
/// authorized, queued, ordered, and bounded like any other input. The sender's identity authorizes it; message text from another
/// agent is untrusted data and never gains instruction authority over the recipient's system or developer instructions.
/// </remarks>
public sealed record AgentMessageRequest
{
    /// <summary>Initializes a validated message request.</summary>
    /// <param name="senderAgentId">The sending agent.</param>
    /// <param name="senderSessionId">The sending agent's session.</param>
    /// <param name="senderRunId">The sending agent's run that produced the message.</param>
    /// <param name="recipientAgentId">The receiving agent.</param>
    /// <param name="recipientSessionId">The receiving agent's existing session.</param>
    /// <param name="goalId">The causal goal, or <see langword="null"/> when the message is not tied to a goal.</param>
    /// <param name="attemptId">The causal goal attempt, or <see langword="null"/>; it requires <paramref name="goalId"/>.</param>
    /// <param name="delivery">How the recipient takes the message: steering at the next safe boundary or as follow-up work.</param>
    /// <param name="parts">The non-empty message content.</param>
    /// <param name="idempotencyKey">The replay identity: the same key with the same content admits once, and with different content conflicts.</param>
    /// <param name="identity">The authenticated identity the message is admitted under.</param>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default, a present optional identity is default, or <paramref name="delivery"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="parts"/> is default, empty, or contains null, <paramref name="idempotencyKey"/> is blank, or an attempt is named without a goal.</exception>
    public AgentMessageRequest(
        AgentId senderAgentId,
        SessionId senderSessionId,
        RunId senderRunId,
        AgentId recipientAgentId,
        SessionId recipientSessionId,
        GoalId? goalId,
        GoalAttemptId? attemptId,
        InputDelivery delivery,
        ImmutableArray<ContentPart> parts,
        IdempotencyKey idempotencyKey,
        ExecutionIdentity identity)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(senderAgentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(senderSessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(senderRunId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(recipientAgentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(recipientSessionId, default);
        if (goalId is { } goal)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(goal, default, nameof(goalId));
        }

        if (attemptId is { } attempt)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(attempt, default, nameof(attemptId));
            ArgumentException.ThrowIfNotEqual(goalId is not null, true, nameof(attemptId));
        }

        ArgumentOutOfRangeException.ThrowIfUndefined(delivery);
        ArgumentException.ThrowIfDefault(parts);
        ArgumentException.ThrowIfNotEqual(parts.IsEmpty, false, nameof(parts));
        ArgumentException.ThrowIfContainsNull(parts);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(identity);
        SenderAgentId = senderAgentId;
        SenderSessionId = senderSessionId;
        SenderRunId = senderRunId;
        RecipientAgentId = recipientAgentId;
        RecipientSessionId = recipientSessionId;
        GoalId = goalId;
        AttemptId = attemptId;
        Delivery = delivery;
        Parts = parts;
        IdempotencyKey = idempotencyKey;
        Identity = identity;
    }

    /// <summary>Gets the sending agent.</summary>
    public AgentId SenderAgentId { get; }

    /// <summary>Gets the sending agent's session.</summary>
    public SessionId SenderSessionId { get; }

    /// <summary>Gets the sending agent's run.</summary>
    public RunId SenderRunId { get; }

    /// <summary>Gets the receiving agent.</summary>
    public AgentId RecipientAgentId { get; }

    /// <summary>Gets the receiving agent's session.</summary>
    public SessionId RecipientSessionId { get; }

    /// <summary>Gets the causal goal, or <see langword="null"/>.</summary>
    public GoalId? GoalId { get; }

    /// <summary>Gets the causal goal attempt, or <see langword="null"/>.</summary>
    public GoalAttemptId? AttemptId { get; }

    /// <summary>Gets how the recipient takes the message.</summary>
    public InputDelivery Delivery { get; }

    /// <summary>Gets the message content.</summary>
    public ImmutableArray<ContentPart> Parts { get; }

    /// <summary>Gets the replay identity.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the authenticated identity the message is admitted under.</summary>
    public ExecutionIdentity Identity { get; }

    /// <inheritdoc/>
    public bool Equals(AgentMessageRequest? other) =>
        other is not null
        && SenderAgentId == other.SenderAgentId && SenderSessionId == other.SenderSessionId && SenderRunId == other.SenderRunId
        && RecipientAgentId == other.RecipientAgentId && RecipientSessionId == other.RecipientSessionId
        && GoalId == other.GoalId && AttemptId == other.AttemptId && Delivery == other.Delivery
        && Parts.SequenceEqual(other.Parts) && IdempotencyKey == other.IdempotencyKey && Identity == other.Identity;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SenderAgentId);
        hash.Add(SenderSessionId);
        hash.Add(SenderRunId);
        hash.Add(RecipientAgentId);
        hash.Add(RecipientSessionId);
        hash.Add(GoalId);
        hash.Add(AttemptId);
        hash.Add(Delivery);
        foreach (var part in Parts)
        {
            hash.Add(part);
        }

        hash.Add(IdempotencyKey);
        hash.Add(Identity);
        return hash.ToHashCode();
    }
}
