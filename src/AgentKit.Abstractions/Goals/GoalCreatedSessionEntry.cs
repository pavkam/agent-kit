// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records in a session's append-only history that one goal was created.</summary>
/// <remarks>
/// <para>
/// The entry carries the goal as created, with no attempts or transitions, plus the creation idempotency key, so replaying
/// the session's entries reconstructs ownership and identity. A delegated child's captured authorization is not session
/// evidence and is never written here; the session-backed projection therefore cannot rediscover a delegation after process
/// loss and reports no intent discovery.
/// </para>
/// </remarks>
public sealed record GoalCreatedSessionEntry: SessionEntry
{
    /// <summary>Initializes a validated goal-created entry.</summary>
    /// <param name="id">The entry identity.</param>
    /// <param name="address">The session that owns the goal; it must be the goal's owning agent and session.</param>
    /// <param name="correlation">The operation that created the goal.</param>
    /// <param name="branchId">The branch the entry is appended to.</param>
    /// <param name="sequence">The entry's position on the branch.</param>
    /// <param name="causalParentId">The preceding entry, or <see langword="null"/>.</param>
    /// <param name="recordedAt">The commit instant.</param>
    /// <param name="schemaVersion">The entry schema version.</param>
    /// <param name="record">The goal as created: no attempts, no transitions, no delegation.</param>
    /// <param name="createKey">The creation idempotency key.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException">The record has attempts, transitions, or a delegation, is not addressed to <paramref name="address"/>, or the key is blank.</exception>
    public GoalCreatedSessionEntry(
        SessionEntryId id,
        SessionAddress address,
        OperationCorrelation correlation,
        BranchId branchId,
        SessionSequence sequence,
        SessionEntryId? causalParentId,
        DateTimeOffset recordedAt,
        SchemaVersion schemaVersion,
        GoalRecord record,
        IdempotencyKey createKey)
        : base(id, address, correlation, branchId, sequence, causalParentId, recordedAt, schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNotEqual(record.Attempts.IsEmpty && record.Transitions.IsEmpty && record.Delegation is null, true, nameof(record));
        ArgumentException.ThrowIfNotEqual(record.Goal.OwnerAgentId, address.AgentId, nameof(record));
        ArgumentException.ThrowIfNotEqual(record.Goal.SessionId, address.SessionId, nameof(record));
        ArgumentException.ThrowIfNullOrWhiteSpace(createKey.Value, nameof(createKey));
        Record = record;
        CreateKey = createKey;
    }

    /// <summary>Gets the goal as created.</summary>
    public GoalRecord Record { get; init; }

    /// <summary>Gets the creation idempotency key.</summary>
    public IdempotencyKey CreateKey { get; init; }
}
