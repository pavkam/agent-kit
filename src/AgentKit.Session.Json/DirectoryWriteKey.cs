// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Keys an idempotent tenant-scoped write to one established session address.</summary>
internal readonly record struct DirectoryWriteKey
{
    /// <summary>Initializes a validated directory-write key.</summary>
    /// <param name="tenantId">The authenticated nonblank tenant identity.</param>
    /// <param name="address">The non-null complete session address.</param>
    /// <param name="idempotencyKey">The nonblank caller retry identity.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    /// <exception cref="ArgumentException">A textual identity is blank.</exception>
    internal DirectoryWriteKey(TenantId tenantId, SessionAddress address, IdempotencyKey idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        TenantId = tenantId;
        Address = address;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the tenant isolating this write.</summary>
    internal TenantId TenantId { get; }
    /// <summary>Gets the complete routed session address.</summary>
    internal SessionAddress Address { get; }
    /// <summary>Gets the caller retry identity.</summary>
    internal IdempotencyKey IdempotencyKey { get; }
}
