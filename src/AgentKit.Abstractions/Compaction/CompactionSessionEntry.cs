// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A session entry carrying one durable <see cref="CompactionRecord"/>.</summary>
/// <remarks>
/// Recording compaction outcomes as ordinary session entries — rather than
/// as separate out-of-band state — means a session's append-only record
/// remains the single source of truth for what happened to it, including
/// when and why it was compacted; replay and audit tooling that walks
/// <see cref="ISessionStore"/> entries see compactions in their true causal
/// position without a side channel.
/// </remarks>
public sealed record CompactionSessionEntry: SessionEntry
{
    /// <summary>Initializes a new instance of the <see cref="CompactionSessionEntry"/> record.</summary>
    /// <param name="id">The stable identity of this entry.</param>
    /// <param name="address">The session this entry belongs to.</param>
    /// <param name="correlation">The causal operation that produced this entry.</param>
    /// <param name="branchId">The branch this entry belongs to.</param>
    /// <param name="sequence">This entry's position within its branch.</param>
    /// <param name="causalParentId">The entry this one causally follows, when applicable.</param>
    /// <param name="recordedAt">The commit time from the injected <see cref="TimeProvider"/>.</param>
    /// <param name="schemaVersion">The durable schema version of this entry.</param>
    /// <param name="record">The durable compaction record carried by this entry.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="record"/> (or a base parameter) is null.
    /// </exception>
    public CompactionSessionEntry(
        SessionEntryId id,
        SessionAddress address,
        OperationCorrelation correlation,
        BranchId branchId,
        SessionSequence sequence,
        SessionEntryId? causalParentId,
        DateTimeOffset recordedAt,
        SchemaVersion schemaVersion,
        CompactionRecord record)
        : base(id, address, correlation, branchId, sequence, causalParentId, recordedAt, schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(record);
        Record = record;
    }

    /// <summary>Gets the durable compaction record carried by this entry.</summary>
    public CompactionRecord Record { get; init; }
}
