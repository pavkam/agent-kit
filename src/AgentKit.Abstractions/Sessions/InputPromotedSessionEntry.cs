// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records exact ordered consumption of admitted inputs into one run turn.</summary>
public sealed record InputPromotedSessionEntry: SessionEntry
{
    /// <summary>Initializes a promotion marker.</summary><param name="id">The stable entry ID.</param><param name="address">The owning session.</param><param name="correlation">The installed in-run correlation.</param><param name="branchId">The selected branch.</param><param name="sequence">The committed promotion sequence.</param><param name="causalParentId">The prior branch tip.</param><param name="recordedAt">The commit timestamp.</param><param name="schemaVersion">The entry schema.</param><param name="executionLaneId">The owning lane.</param><param name="initiatingAdmissionId">The admission that triggered the accepted run.</param><param name="cutoff">The inclusive admission cutoff.</param><param name="admissionIds">The exact source-ordered consumed admissions.</param><exception cref="ArgumentException"><paramref name="admissionIds"/> is invalid or does not contain <paramref name="initiatingAdmissionId"/>.</exception><exception cref="ArgumentOutOfRangeException">The lane or initiating admission is default.</exception>
    public InputPromotedSessionEntry(SessionEntryId id, SessionAddress address, InRunOperationCorrelation correlation,
        BranchId branchId, SessionSequence sequence, SessionEntryId? causalParentId, DateTimeOffset recordedAt,
        SchemaVersion schemaVersion, ExecutionLaneId executionLaneId, AdmissionId initiatingAdmissionId,
        SessionSequence cutoff, ImmutableArray<AdmissionId> admissionIds)
        : base(id, address, correlation, branchId, sequence, causalParentId, recordedAt, schemaVersion)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(initiatingAdmissionId, default);
        ArgumentException.ThrowIfInvalidPromotionAdmissions(admissionIds);
        ArgumentException.ThrowIfDoesNotContain(admissionIds, initiatingAdmissionId, nameof(initiatingAdmissionId));
        ExecutionLaneId = executionLaneId;
        InitiatingAdmissionId = initiatingAdmissionId;
        Cutoff = cutoff;
        AdmissionIds = admissionIds;
    }
    /// <summary>Gets the lane.</summary><value>The non-default lane whose admissions were consumed.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets the admission that triggered the accepted run.</summary><value>A non-default member of <see cref="AdmissionIds"/>.</value>
    public AdmissionId InitiatingAdmissionId { get; }
    /// <summary>Gets the inclusive cutoff.</summary><value>The durable admission sequence used for selection.</value>
    public SessionSequence Cutoff { get; }
    /// <summary>Gets consumed admission identities.</summary><value>The exact nonempty source order.</value>
    public ImmutableArray<AdmissionId> AdmissionIds { get; }
}
