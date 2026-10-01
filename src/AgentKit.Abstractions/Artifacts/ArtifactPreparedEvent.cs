// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes that content was staged and a preparation receipt issued.</summary>
public sealed record ArtifactPreparedEvent: ArtifactEvent
{
    /// <summary>Initializes the event.</summary>
    /// <param name="coordinatorKey">The coordinator that committed the transition.</param>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="profileKey">The logical profile the coordinator is bound to.</param>
    /// <param name="occurredAt">The instant of the transition.</param>
    /// <param name="artifactId">The reserved logical artifact.</param>
    /// <param name="preparationId">The staging identity.</param>
    /// <param name="version">The reserved immutable version.</param>
    /// <exception cref="ArgumentException">A key, tenant, or version is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is empty or an enum is undefined.</exception>
    public ArtifactPreparedEvent(
        ComponentKey<IArtifactCoordinator> coordinatorKey, TenantId tenantId, ArtifactProfileKey profileKey, DateTimeOffset occurredAt,
        ArtifactId artifactId, ArtifactPreparationId preparationId, ArtifactVersion version)
        : base(coordinatorKey, tenantId, profileKey, occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(artifactId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArtifactId = artifactId; PreparationId = preparationId; Version = version;
    }

    /// <summary>Gets the reserved logical artifact.</summary>
    public ArtifactId ArtifactId { get; }

    /// <summary>Gets the staging identity.</summary>
    public ArtifactPreparationId PreparationId { get; }

    /// <summary>Gets the reserved immutable version.</summary>
    public ArtifactVersion Version { get; }
}
