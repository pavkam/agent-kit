// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries exact deletion authority to the backend owning committed bytes.</summary>
public sealed record ArtifactStoreDeleteRequest
{
    /// <summary>Initializes an exact backend deletion request.</summary>
    /// <param name="reference">The exact portable reference.</param>
    /// <param name="scope">The exact authorization scope.</param>
    /// <param name="identity">The authenticated identity.</param>
    /// <param name="grant">The single-use deletion grant.</param>
    /// <param name="idempotencyKey">The caller-owned replay key.</param>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="idempotencyKey"/> is blank.</exception>
    public ArtifactStoreDeleteRequest(ArtifactReference reference, SecurityAuthorizationScope scope, ExecutionIdentity identity, SecurityGrant grant, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        Reference = reference;
        Scope = scope;
        Identity = identity;
        Grant = grant;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the exact portable reference.</summary>
    public ArtifactReference Reference { get; }
    /// <summary>Gets the exact authorization scope.</summary>
    public SecurityAuthorizationScope Scope { get; }
    /// <summary>Gets the authenticated identity.</summary>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets the single-use deletion grant.</summary>
    public SecurityGrant Grant { get; }
    /// <summary>Gets the caller-owned replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
