// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries exact deletion authority to the backend owning committed bytes.</summary>
public sealed record ArtifactStoreDeleteRequest
{
    /// <summary>Initializes an exact backend deletion request.</summary>
    /// <param name="reference">The exact portable reference.</param>
    /// <param name="grant">The single-use grant whose captured authorization carries the exact scope and identity of the operation.</param>
    /// <param name="idempotencyKey">The caller-owned replay key.</param>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="idempotencyKey"/> is blank or <paramref name="grant"/> carries no captured authorization.</exception>
    public ArtifactStoreDeleteRequest(ArtifactReference reference, SecurityGrant grant, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        Reference = reference; Grant = grant; IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the exact portable reference.</summary>
    public ArtifactReference Reference { get; }

    /// <summary>Gets the single-use deletion grant.</summary>
    public SecurityGrant Grant { get; }

    /// <summary>Gets the exact security scope bound by the grant.</summary>
    public SecurityAuthorizationScope Scope => Grant.Scope;

    /// <summary>Gets the authenticated identity bound by the grant.</summary>
    public ExecutionIdentity Identity => Grant.Identity;

    /// <summary>Gets the caller-owned replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
