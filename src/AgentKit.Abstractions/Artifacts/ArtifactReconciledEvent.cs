// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes that reconciliation established a terminal disposition for a reference-commit intent.</summary>
public sealed record ArtifactReconciledEvent: ArtifactEvent
{
    /// <summary>Initializes the event.</summary>
    /// <param name="coordinatorKey">The coordinator that committed the transition.</param>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="profileKey">The logical profile the coordinator is bound to.</param>
    /// <param name="occurredAt">The instant of the transition.</param>
    /// <param name="preparationId">The preparation whose intent was reconciled.</param>
    /// <param name="disposition">The established disposition.</param>
    /// <exception cref="ArgumentException">A key, tenant, or version is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is empty or an enum is undefined.</exception>
    public ArtifactReconciledEvent(
        ComponentKey<IArtifactCoordinator> coordinatorKey, TenantId tenantId, ArtifactProfileKey profileKey, DateTimeOffset occurredAt,
        ArtifactPreparationId preparationId, ArtifactReconciliationDisposition disposition)
        : base(coordinatorKey, tenantId, profileKey, occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(disposition);
        PreparationId = preparationId; Disposition = disposition;
    }

    /// <summary>Gets the preparation whose intent was reconciled.</summary>
    public ArtifactPreparationId PreparationId { get; }

    /// <summary>Gets the established disposition.</summary>
    public ArtifactReconciliationDisposition Disposition { get; }
}
