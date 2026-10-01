// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is one immutable, content-free observation of a committed artifact lifecycle transition.</summary>
/// <remarks>Events carry identities and bounded outcomes, never bytes, media types, locators, or metadata values. Sinks observe events and cannot affect lifecycle results.</remarks>
public abstract record ArtifactEvent
{
    /// <summary>Initializes the shared event identity.</summary>
    /// <param name="coordinatorKey">The coordinator that committed the transition.</param>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="profileKey">The logical profile the coordinator is bound to.</param>
    /// <param name="occurredAt">The instant of the transition.</param>
    /// <exception cref="ArgumentException">A key or tenant is blank.</exception>
    private protected ArtifactEvent(
        ComponentKey<IArtifactCoordinator> coordinatorKey, TenantId tenantId, ArtifactProfileKey profileKey, DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coordinatorKey.Value, nameof(coordinatorKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        CoordinatorKey = coordinatorKey;
        TenantId = tenantId;
        ProfileKey = profileKey;
        OccurredAt = occurredAt;
    }

    /// <summary>Gets the coordinator that committed the transition.</summary>
    public ComponentKey<IArtifactCoordinator> CoordinatorKey { get; }

    /// <summary>Gets the tenant partition.</summary>
    public TenantId TenantId { get; }

    /// <summary>Gets the logical profile the coordinator is bound to.</summary>
    public ArtifactProfileKey ProfileKey { get; }

    /// <summary>Gets the instant of the transition.</summary>
    public DateTimeOffset OccurredAt { get; }
}
