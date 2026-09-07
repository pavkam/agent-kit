// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan;

/// <summary>Records one immutable current-plan revision in a session's append-only history.</summary>
public sealed record PlanSessionEntry: SessionEntry
{
    /// <summary>Initializes one durable plan revision entry.</summary>
    /// <param name="id">The stable entry identity.</param>
    /// <param name="address">The containing session.</param>
    /// <param name="correlation">The causing operation.</param>
    /// <param name="branchId">The containing branch.</param>
    /// <param name="sequence">The exact branch sequence.</param>
    /// <param name="causalParentId">The preceding plan revision entry, when known.</param>
    /// <param name="recordedAt">The commit timestamp.</param>
    /// <param name="schemaVersion">The entry schema version.</param>
    /// <param name="plan">The immutable current-plan snapshot.</param>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> or a base argument is null.</exception>
    public PlanSessionEntry(
        SessionEntryId id,
        SessionAddress address,
        OperationCorrelation correlation,
        BranchId branchId,
        SessionSequence sequence,
        SessionEntryId? causalParentId,
        DateTimeOffset recordedAt,
        SchemaVersion schemaVersion,
        WorkPlan plan)
        : base(id, address, correlation, branchId, sequence, causalParentId, recordedAt, schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Plan = plan;
    }

    /// <summary>Gets the immutable current-plan snapshot.</summary>
    public WorkPlan Plan { get; init; }
}
