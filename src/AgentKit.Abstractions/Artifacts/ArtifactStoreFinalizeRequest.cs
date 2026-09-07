// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries exact publication authority to the backend owning staged bytes.</summary>
public sealed record ArtifactStoreFinalizeRequest
{
    /// <summary>Initializes a validated backend publication request.</summary>
    /// <param name="preparationId">The staging identity.</param><param name="scope">The exact authorization scope.</param>
    /// <param name="identity">The authenticated identity.</param><param name="grant">The single-use publication grant.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="preparationId"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="idempotencyKey"/> is blank.</exception>
    public ArtifactStoreFinalizeRequest(ArtifactPreparationId preparationId, SecurityAuthorizationScope scope, ExecutionIdentity identity, SecurityGrant grant, IdempotencyKey idempotencyKey)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentNullException.ThrowIfNull(scope); ArgumentNullException.ThrowIfNull(identity); ArgumentNullException.ThrowIfNull(grant);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        PreparationId = preparationId; Scope = scope; Identity = identity; Grant = grant; IdempotencyKey = idempotencyKey;
    }
    /// <summary>Gets the staging identity.</summary>
    public ArtifactPreparationId PreparationId { get; }
    /// <summary>Gets the exact authorization scope.</summary>
    public SecurityAuthorizationScope Scope { get; }
    /// <summary>Gets the authenticated identity.</summary>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets the single-use publication grant.</summary>
    public SecurityGrant Grant { get; }
    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
