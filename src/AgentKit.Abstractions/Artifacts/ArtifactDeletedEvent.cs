// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes that one committed version was tombstoned or found already deleted.</summary>
public sealed record ArtifactDeletedEvent: ArtifactEvent
{
    /// <summary>Initializes the event.</summary>
    /// <param name="coordinatorKey">The coordinator that committed the transition.</param>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="profileKey">The logical profile the coordinator is bound to.</param>
    /// <param name="occurredAt">The instant of the transition.</param>
    /// <param name="artifactId">The logical artifact.</param>
    /// <param name="version">The immutable version.</param>
    /// <param name="alreadyAbsent">Whether the version was already deleted.</param>
    /// <exception cref="ArgumentException">A key, tenant, or version is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is empty or an enum is undefined.</exception>
    public ArtifactDeletedEvent(
        ComponentKey<IArtifactCoordinator> coordinatorKey, TenantId tenantId, ArtifactProfileKey profileKey, DateTimeOffset occurredAt,
        ArtifactId artifactId, ArtifactVersion version, bool alreadyAbsent)
        : base(coordinatorKey, tenantId, profileKey, occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(artifactId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArtifactId = artifactId; Version = version; AlreadyAbsent = alreadyAbsent;
    }

    /// <summary>Gets the logical artifact.</summary>
    public ArtifactId ArtifactId { get; }

    /// <summary>Gets the immutable version.</summary>
    public ArtifactVersion Version { get; }

    /// <summary>Gets whether the version was already deleted.</summary>
    public bool AlreadyAbsent { get; }
}
