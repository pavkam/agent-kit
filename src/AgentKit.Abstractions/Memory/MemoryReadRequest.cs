// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a memory store to read one record by identity.</summary>
/// <remarks>The store resolves the identity only inside the authorized tenant, agent, and principal visibility. An item outside that scope is reported as not found so its existence is never revealed.</remarks>
public sealed record MemoryReadRequest
{
    /// <summary>Initializes a validated read request.</summary>
    /// <param name="id">The memory identity.</param>
    /// <param name="grant">The single-use grant for this exact read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    /// <exception cref="ArgumentException">The grant lacks captured authorization.</exception>
    public MemoryReadRequest(MemoryId id, SecurityGrant grant)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        Id = id;
        Grant = grant;
    }

    /// <summary>Gets the memory identity.</summary>
    public MemoryId Id { get; }

    /// <summary>Gets the single-use grant for this exact read.</summary>
    public SecurityGrant Grant { get; }
}
