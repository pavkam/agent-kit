// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes that a preparation was atomically published as an immutable committed version.</summary>
public sealed record ArtifactFinalizedEvent: ArtifactEvent
{
    /// <summary>Initializes the event.</summary>
    /// <param name="coordinatorKey">The coordinator that committed the transition.</param>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="profileKey">The logical profile the coordinator is bound to.</param>
    /// <param name="occurredAt">The instant of the transition.</param>
    /// <param name="artifactId">The published logical artifact.</param>
    /// <param name="version">The published immutable version.</param>
    /// <param name="preparationId">The staging identity that was published.</param>
    /// <exception cref="ArgumentException">A key, tenant, or version is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is empty or an enum is undefined.</exception>
    public ArtifactFinalizedEvent(
        ComponentKey<IArtifactCoordinator> coordinatorKey, TenantId tenantId, ArtifactProfileKey profileKey, DateTimeOffset occurredAt,
        ArtifactId artifactId, ArtifactVersion version, ArtifactPreparationId preparationId)
        : base(coordinatorKey, tenantId, profileKey, occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(artifactId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArtifactId = artifactId; Version = version; PreparationId = preparationId;
    }

    /// <summary>Gets the published logical artifact.</summary>
    public ArtifactId ArtifactId { get; }

    /// <summary>Gets the published immutable version.</summary>
    public ArtifactVersion Version { get; }

    /// <summary>Gets the staging identity that was published.</summary>
    public ArtifactPreparationId PreparationId { get; }
}
