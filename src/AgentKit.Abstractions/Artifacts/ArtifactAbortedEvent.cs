// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes that unpublished staging was aborted or found already absent.</summary>
public sealed record ArtifactAbortedEvent: ArtifactEvent
{
    /// <summary>Initializes the event.</summary>
    /// <param name="coordinatorKey">The coordinator that committed the transition.</param>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="profileKey">The logical profile the coordinator is bound to.</param>
    /// <param name="occurredAt">The instant of the transition.</param>
    /// <param name="preparationId">The staging identity.</param>
    /// <param name="reason">The declared abort reason.</param>
    /// <param name="alreadyAbsent">Whether the staging was already absent.</param>
    /// <exception cref="ArgumentException">A key, tenant, or version is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is empty or an enum is undefined.</exception>
    public ArtifactAbortedEvent(
        ComponentKey<IArtifactCoordinator> coordinatorKey, TenantId tenantId, ArtifactProfileKey profileKey, DateTimeOffset occurredAt,
        ArtifactPreparationId preparationId, ArtifactAbortReason reason, bool alreadyAbsent)
        : base(coordinatorKey, tenantId, profileKey, occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(reason);
        PreparationId = preparationId; Reason = reason; AlreadyAbsent = alreadyAbsent;
    }

    /// <summary>Gets the staging identity.</summary>
    public ArtifactPreparationId PreparationId { get; }

    /// <summary>Gets the declared abort reason.</summary>
    public ArtifactAbortReason Reason { get; }

    /// <summary>Gets whether the staging was already absent.</summary>
    public bool AlreadyAbsent { get; }
}
