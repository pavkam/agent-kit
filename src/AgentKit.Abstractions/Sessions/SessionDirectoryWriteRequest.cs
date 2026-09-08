// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests the conditional, idempotent recording of one session location.</summary>
/// <remarks>The context must name the same agent and session as the location. The directory verifies the full identity, tenant, grant, and effect evidence before committing the route.</remarks>
public sealed record SessionDirectoryWriteRequest
{
    /// <summary>Initializes a location-recording request.</summary>
    /// <param name="context">The non-null operation context performing the record.</param>
    /// <param name="location">The non-null proposed tenant-partitioned location.</param>
    /// <param name="idempotencyKey">The nonblank key that makes retries safe.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="location"/> is null.</exception>
    /// <exception cref="ArgumentException">The context address or tenant does not match <paramref name="location"/>.</exception>
    public SessionDirectoryWriteRequest(SessionOperationContext context, SessionLocation location, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(location);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        ArgumentException.ThrowIfInvalidSessionDirectoryWriteBinding(context, location, nameof(location));
        Context = context; Location = location; IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the operation context performing the record.</summary><value>The non-null context matching the location address and tenant.</value>
    public SessionOperationContext Context { get; }
    /// <summary>Gets the proposed location.</summary><value>The non-null route to record conditionally.</value>
    public SessionLocation Location { get; }
    /// <summary>Gets the retry identity.</summary><value>The nonblank idempotency key.</value>
    public IdempotencyKey IdempotencyKey { get; }
}
