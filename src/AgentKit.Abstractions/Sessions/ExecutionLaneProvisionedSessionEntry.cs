// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records the exact branch, profile, and configuration evidence used to provision one execution lane.</summary>
/// <remarks>The retained profile and configuration explain this provisioning transaction. They do not prohibit a later run from adopting a different explicitly validated snapshot.</remarks>
public sealed record ExecutionLaneProvisionedSessionEntry
    : SessionEntry
{
    /// <summary>Initializes an execution-lane provisioning fact.</summary>
    /// <param name="id">The globally unique entry identity.</param><param name="address">The owning session.</param><param name="correlation">The before-run provisioning cause.</param><param name="branchId">The branch claimed by the lane.</param><param name="sequence">This entry's 1-based commit position within <paramref name="branchId"/>.</param><param name="causalParentId">The branch tip before provisioning.</param><param name="recordedAt">The commit timestamp.</param><param name="schemaVersion">The entry schema.</param><param name="executionLaneId">The lane installed by this fact.</param><param name="laneRevision">The installed positive lane revision.</param><param name="sessionProfile">The profile selected for provisioning.</param><param name="configuration">The configuration selected for provisioning.</param>
    /// <exception cref="ArgumentOutOfRangeException">The lane or revision is default.</exception><exception cref="ArgumentNullException">A retained reference is null.</exception><exception cref="ArgumentException">Configuration and correlation authorization versions differ.</exception>
    public ExecutionLaneProvisionedSessionEntry(SessionEntryId id, SessionAddress address,
        BeforeRunOperationCorrelation correlation, BranchId branchId, SessionSequence sequence,
        SessionEntryId? causalParentId, DateTimeOffset recordedAt, SchemaVersion schemaVersion,
        ExecutionLaneId executionLaneId, SessionLaneRevision laneRevision,
        SessionProfileReference sessionProfile, RunConfigurationReference configuration)
        : base(id, address, correlation, branchId, sequence, causalParentId, recordedAt, schemaVersion)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(laneRevision, default);
        ArgumentNullException.ThrowIfNull(sessionProfile);
        ArgumentNullException.ThrowIfNull(configuration);
        ExecutionLaneId = executionLaneId;
        LaneRevision = laneRevision;
        SessionProfile = sessionProfile;
        Configuration = configuration;
    }

    /// <summary>Gets the installed lane.</summary><value>The non-default lane identity that now owns this branch.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets the installed lane revision.</summary><value>The initial positive lane compare-and-swap revision.</value>
    public SessionLaneRevision LaneRevision { get; }
    /// <summary>Gets provisioning profile evidence.</summary><value>The exact retained session profile selected for this transaction.</value>
    public SessionProfileReference SessionProfile { get; }
    /// <summary>Gets provisioning configuration evidence.</summary><value>The exact retained run configuration selected for this transaction.</value>
    public RunConfigurationReference Configuration { get; }
}
