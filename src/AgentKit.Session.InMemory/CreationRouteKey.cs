// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>Keys one tenant-scoped logical creation attempt before a session identity exists.</summary>
internal readonly record struct CreationRouteKey
{
    /// <summary>Initializes a validated creation-route key.</summary>
    /// <param name="tenantId">The authenticated nonblank tenant identity.</param>
    /// <param name="agentId">The non-default owning agent.</param>
    /// <param name="idempotencyKey">The nonblank caller retry identity.</param>
    /// <exception cref="ArgumentException">A textual identity is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> is default.</exception>
    internal CreationRouteKey(TenantId tenantId, AgentId agentId, IdempotencyKey idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        TenantId = tenantId;
        AgentId = agentId;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the tenant isolating this key.</summary>
    internal TenantId TenantId { get; }
    /// <summary>Gets the agent whose session is being created.</summary>
    internal AgentId AgentId { get; }
    /// <summary>Gets the caller retry identity.</summary>
    internal IdempotencyKey IdempotencyKey { get; }
}
