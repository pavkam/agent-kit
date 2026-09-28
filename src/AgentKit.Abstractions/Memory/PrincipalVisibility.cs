// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares which principals may observe durable memory or document content.</summary>
public sealed record PrincipalVisibility
{
    /// <summary>Initializes visibility for one owning principal within a tenant.</summary>
    /// <param name="tenantId">The tenant that owns the content.</param>
    /// <param name="ownerPrincipalId">The principal that owns the content.</param>
    /// <param name="sharedWithTenant">When <see langword="true"/>, any principal in the tenant may read subject to authorization.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    public PrincipalVisibility(TenantId tenantId, PrincipalId ownerPrincipalId, bool sharedWithTenant = false)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, default, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfEqual(ownerPrincipalId, default, nameof(ownerPrincipalId));
        TenantId = tenantId;
        OwnerPrincipalId = ownerPrincipalId;
        SharedWithTenant = sharedWithTenant;
    }

    /// <summary>Gets the tenant that owns the content.</summary>
    public TenantId TenantId { get; init; }

    /// <summary>Gets the principal that owns the content.</summary>
    public PrincipalId OwnerPrincipalId { get; init; }

    /// <summary>Gets whether any principal in the tenant may read subject to authorization.</summary>
    public bool SharedWithTenant { get; init; }
}
