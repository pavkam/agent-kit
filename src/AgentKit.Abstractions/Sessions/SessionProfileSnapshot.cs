// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures all behaviorally relevant session choices for one compiled operation.</summary>
/// <remarks>The snapshot is immutable configuration evidence. It binds no live services and does not itself authorize directory or store access.</remarks>
public sealed record SessionProfileSnapshot
{
    /// <summary>Initializes a validated immutable session-profile snapshot.</summary>
    /// <param name="reference">The selected profile identity and revision.</param>
    /// <param name="coordinatorKey">The exact session-coordinator registration key.</param>
    /// <param name="runCoordinatorKey">The exact session-run-coordinator registration key.</param>
    /// <param name="defaultStoreKey">The explicit initial store selection.</param>
    /// <param name="requiredStoreCapabilities">The optional store operations required by the profile.</param>
    /// <param name="requiresDurableStore">Whether the selected store must survive process loss.</param>
    /// <param name="requiresDistributedFencing">Whether the selected store must support distributed lane fencing.</param>
    /// <param name="retentionProfile">The selected retention policy key.</param>
    /// <param name="busyBehavior">The lane-busy acquisition behavior.</param>
    /// <param name="maximumAppendEntries">The positive append batch ceiling.</param>
    /// <param name="maximumPageSize">The positive read-page ceiling.</param>
    /// <param name="verifySnapshotHashes">Whether loads verify snapshot integrity before use.</param>
    /// <param name="deleteOnDispose">Whether the owning host requests deletion during disposal.</param>
    /// <param name="configurationFingerprint">The nonblank compiled-configuration fingerprint.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    /// <exception cref="ArgumentException">A selected component, store, retention, or fingerprint key is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A limit is not positive, a capability flag is unknown, or <paramref name="busyBehavior"/> is undefined.</exception>
    public SessionProfileSnapshot(
        SessionProfileReference reference,
        ComponentKey<ISessionCoordinator> coordinatorKey,
        ComponentKey<ISessionRunCoordinator> runCoordinatorKey,
        SessionStoreKey defaultStoreKey,
        SessionStoreCapabilities requiredStoreCapabilities,
        bool requiresDurableStore,
        bool requiresDistributedFencing,
        SessionRetentionProfileKey retentionProfile,
        SessionBusyBehavior busyBehavior,
        int maximumAppendEntries,
        int maximumPageSize,
        bool verifySnapshotHashes,
        bool deleteOnDispose,
        ContentHash configurationFingerprint)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(coordinatorKey.Value, nameof(coordinatorKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(runCoordinatorKey.Value, nameof(runCoordinatorKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultStoreKey.Value, nameof(defaultStoreKey));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            (uint) (requiredStoreCapabilities & ~_knownCapabilities),
            0U,
            nameof(requiredStoreCapabilities));
        ArgumentException.ThrowIfNullOrWhiteSpace(retentionProfile.Value, nameof(retentionProfile));
        ArgumentOutOfRangeException.ThrowIfUndefined(busyBehavior);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumAppendEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPageSize);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationFingerprint.Value, nameof(configurationFingerprint));
        Reference = reference;
        CoordinatorKey = coordinatorKey;
        RunCoordinatorKey = runCoordinatorKey;
        DefaultStoreKey = defaultStoreKey;
        RequiredStoreCapabilities = requiredStoreCapabilities;
        RequiresDurableStore = requiresDurableStore;
        RequiresDistributedFencing = requiresDistributedFencing;
        RetentionProfile = retentionProfile;
        BusyBehavior = busyBehavior;
        MaximumAppendEntries = maximumAppendEntries;
        MaximumPageSize = maximumPageSize;
        VerifySnapshotHashes = verifySnapshotHashes;
        DeleteOnDispose = deleteOnDispose;
        ConfigurationFingerprint = configurationFingerprint;
    }

    private const SessionStoreCapabilities _knownCapabilities =
        SessionStoreCapabilities.Branching | SessionStoreCapabilities.Snapshots |
        SessionStoreCapabilities.Retention | SessionStoreCapabilities.Transactions;

    /// <summary>Gets the selected immutable profile identity.</summary><value>The exact profile key and positive revision.</value>
    public SessionProfileReference Reference { get; }
    /// <summary>Gets the selected session-coordinator registration key.</summary><value>A nonblank key resolved by composition.</value>
    public ComponentKey<ISessionCoordinator> CoordinatorKey { get; }
    /// <summary>Gets the selected session-run-coordinator registration key.</summary><value>A nonblank key resolved by composition.</value>
    public ComponentKey<ISessionRunCoordinator> RunCoordinatorKey { get; }
    /// <summary>Gets the explicit store selected for new sessions.</summary><value>A nonblank store key; existing sessions use their directory location.</value>
    public SessionStoreKey DefaultStoreKey { get; }
    /// <summary>Gets the optional store operations required by this profile.</summary><value>Only defined capability flags that the selected store must advertise.</value>
    public SessionStoreCapabilities RequiredStoreCapabilities { get; }
    /// <summary>Gets whether the profile requires a durable store.</summary><value><see langword="true"/> when process-local storage is incompatible.</value>
    public bool RequiresDurableStore { get; }
    /// <summary>Gets whether the profile requires distributed lane fencing.</summary><value><see langword="true"/> when the selected store must provide fencing support.</value>
    public bool RequiresDistributedFencing { get; }
    /// <summary>Gets the retention policy selection.</summary><value>A nonblank policy key.</value>
    public SessionRetentionProfileKey RetentionProfile { get; }
    /// <summary>Gets the lane-busy acquisition behavior.</summary><value>The configured defined behavior.</value>
    public SessionBusyBehavior BusyBehavior { get; }
    /// <summary>Gets the append batch ceiling.</summary><value>A positive maximum count.</value>
    public int MaximumAppendEntries { get; }
    /// <summary>Gets the read-page ceiling.</summary><value>A positive maximum count.</value>
    public int MaximumPageSize { get; }
    /// <summary>Gets whether snapshots require integrity verification.</summary><value><see langword="true"/> when verification is required.</value>
    public bool VerifySnapshotHashes { get; }
    /// <summary>Gets whether the owning host requests disposal deletion.</summary><value><see langword="true"/> when deletion is requested.</value>
    public bool DeleteOnDispose { get; }
    /// <summary>Gets the compiled configuration fingerprint.</summary><value>Nonblank immutable evidence of the selected configuration.</value>
    public ContentHash ConfigurationFingerprint { get; }
}
