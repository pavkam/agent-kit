// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a newly installed lane or an equivalent prior provisioning commit.</summary>
public sealed record SessionExecutionLaneProvisioned
    : SessionExecutionLaneProvisionResult
{
    /// <summary>Initializes successful lane-provision evidence.</summary>
    /// <param name="executionLaneId">The installed lane.</param><param name="branchCursor">The committed lane-owned branch tip.</param><param name="laneRevision">The positive installed lane revision.</param><param name="sessionVersion">The canonical session version after the commit.</param><param name="existing">Whether an equivalent prior commit was reconciled.</param>
    /// <exception cref="ArgumentOutOfRangeException">A supplied identity or revision is default.</exception><exception cref="ArgumentNullException"><paramref name="branchCursor"/> is null.</exception>
    public SessionExecutionLaneProvisioned(ExecutionLaneId executionLaneId, SessionBranchCursor branchCursor,
        SessionLaneRevision laneRevision, SessionVersion sessionVersion, bool existing)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default);
        ArgumentNullException.ThrowIfNull(branchCursor);
        ArgumentOutOfRangeException.ThrowIfEqual(laneRevision, default);
        ExecutionLaneId = executionLaneId;
        BranchCursor = branchCursor;
        LaneRevision = laneRevision;
        SessionVersion = sessionVersion;
        Existing = existing;
    }

    /// <summary>Gets the installed lane.</summary><value>The non-default lane identity.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets the committed lane cursor.</summary><value>The provisioning entry at the lane-owned branch tip.</value>
    public SessionBranchCursor BranchCursor { get; }
    /// <summary>Gets the installed lane revision.</summary><value>The positive revision for subsequent lane compare-and-swap.</value>
    public SessionLaneRevision LaneRevision { get; }
    /// <summary>Gets the committed session version.</summary><value>The canonical whole-session version after provisioning.</value>
    public SessionVersion SessionVersion { get; }
    /// <summary>Gets whether a prior equivalent commit was returned.</summary><value><see langword="true"/> for replay; otherwise <see langword="false"/>.</value>
    public bool Existing { get; }
}
