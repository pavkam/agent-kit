// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries exact publication authority to the backend owning staged bytes.</summary>
public sealed record ArtifactStoreFinalizeRequest
{
    /// <summary>Initializes a validated backend publication request.</summary>
    /// <param name="preparationId">The staging identity.</param>
    /// <param name="grant">The single-use grant whose captured authorization carries the exact scope and identity of the operation.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="preparationId"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="idempotencyKey"/> is blank or <paramref name="grant"/> carries no captured authorization.</exception>
    public ArtifactStoreFinalizeRequest(ArtifactPreparationId preparationId, SecurityGrant grant, IdempotencyKey idempotencyKey)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        PreparationId = preparationId; Grant = grant; IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the staging identity.</summary>
    public ArtifactPreparationId PreparationId { get; }

    /// <summary>Gets the single-use publication grant.</summary>
    public SecurityGrant Grant { get; }

    /// <summary>Gets the exact security scope bound by the grant.</summary>
    public SecurityAuthorizationScope Scope => Grant.Scope;

    /// <summary>Gets the authenticated identity bound by the grant.</summary>
    public ExecutionIdentity Identity => Grant.Identity;

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
