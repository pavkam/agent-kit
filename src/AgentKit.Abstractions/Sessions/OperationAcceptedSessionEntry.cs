// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records complete initial total state for one durably accepted operation.</summary>
public sealed record OperationAcceptedSessionEntry: SessionEntry
{
    /// <summary>Initializes an accepted-operation fact.</summary><param name="id">The stable entry ID.</param><param name="address">The owning session.</param><param name="correlation">The accepted in-run correlation.</param><param name="branchId">The selected branch.</param><param name="sequence">The commit sequence.</param><param name="causalParentId">The final initial-message entry.</param><param name="recordedAt">The acceptance timestamp.</param><param name="schemaVersion">The entry schema.</param><param name="state">The complete accepted state whose cursor ends at this entry.</param><exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception><exception cref="ArgumentException">The state and entry correlation or cursor differ, or the accepted entry reuses a materialized identity or parents itself.</exception>
    public OperationAcceptedSessionEntry(SessionEntryId id, SessionAddress address, InRunOperationCorrelation correlation,
        BranchId branchId, SessionSequence sequence, SessionEntryId? causalParentId, DateTimeOffset recordedAt,
        SchemaVersion schemaVersion, SessionAcceptedRunState state)
        : base(id, address, correlation, branchId, sequence, causalParentId, recordedAt, schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNotEqual(state.Address, address, nameof(state));
        ArgumentException.ThrowIfNotEqual(state.Correlation, correlation, nameof(state));
        ArgumentException.ThrowIfNotEqual(state.MaterializedEntryIds.Contains(id), false, nameof(id));
        ArgumentException.ThrowIfNotEqual(state.CommittedCursor.BranchId, branchId, nameof(state));
        ArgumentException.ThrowIfNotEqual(state.CommittedCursor.LastEntryId, id, nameof(state));
        ArgumentException.ThrowIfNotEqual(state.AcceptedAt, recordedAt, nameof(state));
        ArgumentException.ThrowIfNotEqual(causalParentId == id, false, nameof(causalParentId));
        ArgumentException.ThrowIfNotEqual(causalParentId, state.MaterializedEntryIds[^1], nameof(state));
        State = state;
    }
    /// <summary>Gets complete accepted state.</summary><value>The immutable recovery source of truth for this operation.</value>
    public SessionAcceptedRunState State { get; }
}
