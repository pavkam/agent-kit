// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries exact staging-removal authority to the backend.</summary>
public sealed record ArtifactStoreAbortRequest
{
    /// <summary>Initializes a validated backend abort request.</summary>
    /// <param name="preparationId">The staging identity.</param><param name="reason">The abort reason.</param>
    /// <param name="scope">The exact authorization scope.</param><param name="identity">The authenticated identity.</param>
    /// <param name="grant">The single-use removal grant.</param><param name="idempotencyKey">The replay key.</param>
    /// <exception cref="ArgumentOutOfRangeException">The identity is empty or reason is undefined.</exception>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="idempotencyKey"/> is blank.</exception>
    public ArtifactStoreAbortRequest(ArtifactPreparationId preparationId, ArtifactAbortReason reason, SecurityAuthorizationScope scope, ExecutionIdentity identity, SecurityGrant grant, IdempotencyKey idempotencyKey)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default); ArgumentOutOfRangeException.ThrowIfUndefined(reason);
        ArgumentNullException.ThrowIfNull(scope); ArgumentNullException.ThrowIfNull(identity); ArgumentNullException.ThrowIfNull(grant);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        PreparationId = preparationId; Reason = reason; Scope = scope; Identity = identity; Grant = grant; IdempotencyKey = idempotencyKey;
    }
    /// <summary>Gets the staging identity.</summary>
    public ArtifactPreparationId PreparationId { get; }
    /// <summary>Gets the abort reason.</summary>
    public ArtifactAbortReason Reason { get; }
    /// <summary>Gets the exact authorization scope.</summary>
    public SecurityAuthorizationScope Scope { get; }
    /// <summary>Gets the authenticated identity.</summary>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets the single-use removal grant.</summary>
    public SecurityGrant Grant { get; }
    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
