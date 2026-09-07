// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory;

/// <summary>Keys staging lifecycle state within one validated authenticated tenant partition.</summary>
internal readonly record struct TenantArtifactPreparationKey
{
    /// <summary>Initializes a tenant-qualified preparation key.</summary>
    /// <param name="tenantId">The non-empty authenticated tenant that owns the state.</param>
    /// <param name="preparationId">The non-empty preparation identity within that tenant.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tenantId"/> has a null value.</exception>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> has an empty or whitespace value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="preparationId"/> is empty.</exception>
    internal TenantArtifactPreparationKey(TenantId tenantId, ArtifactPreparationId preparationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        TenantId = tenantId;
        PreparationId = preparationId;
    }

    /// <summary>Gets the authenticated tenant that owns the staging state.</summary>
    /// <value>The validated tenant, or the default tenant only on a zero-initialized, invalid default key.</value>
    internal TenantId TenantId { get; }

    /// <summary>Gets the preparation identity within the tenant.</summary>
    /// <value>The validated preparation identity, or the empty identity only on a zero-initialized, invalid default key.</value>
    internal ArtifactPreparationId PreparationId { get; }
}
