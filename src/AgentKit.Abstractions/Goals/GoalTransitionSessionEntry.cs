// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records in a session's append-only history that one goal changed status.</summary>
/// <remarks>The entry carries the transition, its atomic attempt mutation, and the store position at which a settling transition was assigned, so replay reconstructs status, ownership, attempts, and join order from entries alone.</remarks>
public sealed record GoalTransitionSessionEntry: SessionEntry
{
    /// <summary>Initializes a validated goal-transition entry.</summary>
    /// <param name="id">The entry identity.</param>
    /// <param name="address">The session that owns the goal; it must be the transition's owning agent and session.</param>
    /// <param name="correlation">The operation that applied the transition.</param>
    /// <param name="branchId">The branch the entry is appended to.</param>
    /// <param name="sequence">The entry's position on the branch.</param>
    /// <param name="causalParentId">The preceding entry, or <see langword="null"/>.</param>
    /// <param name="recordedAt">The commit instant.</param>
    /// <param name="schemaVersion">The entry schema version.</param>
    /// <param name="transition">The applied transition.</param>
    /// <param name="attempt">The atomic attempt mutation, or <see langword="null"/>.</param>
    /// <param name="settledSequence">The positive settlement sequence assigned, or <see langword="null"/> when the transition did not settle the goal.</param>
    /// <exception cref="ArgumentNullException"><paramref name="transition"/> is null.</exception>
    /// <exception cref="ArgumentException">The transition is not addressed to <paramref name="address"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settledSequence"/> is present and not positive.</exception>
    public GoalTransitionSessionEntry(
        SessionEntryId id,
        SessionAddress address,
        OperationCorrelation correlation,
        BranchId branchId,
        SessionSequence sequence,
        SessionEntryId? causalParentId,
        DateTimeOffset recordedAt,
        SchemaVersion schemaVersion,
        GoalTransition transition,
        GoalAttemptChange? attempt,
        long? settledSequence)
        : base(id, address, correlation, branchId, sequence, causalParentId, recordedAt, schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentException.ThrowIfNotEqual(transition.OwnerAgentId, address.AgentId, nameof(transition));
        ArgumentException.ThrowIfNotEqual(transition.SessionId, address.SessionId, nameof(transition));
        if (settledSequence is { } settled)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settled, nameof(settledSequence));
        }

        Transition = transition;
        Attempt = attempt;
        SettledSequence = settledSequence;
    }

    /// <summary>Gets the applied transition.</summary>
    public GoalTransition Transition { get; init; }

    /// <summary>Gets the atomic attempt mutation, or <see langword="null"/>.</summary>
    public GoalAttemptChange? Attempt { get; init; }

    /// <summary>Gets the settlement sequence assigned, or <see langword="null"/>.</summary>
    public long? SettledSequence { get; init; }
}
