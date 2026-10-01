// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries exact staging-removal authority to the backend.</summary>
public sealed record ArtifactStoreAbortRequest
{
    /// <summary>Initializes a validated backend abort request.</summary>
    /// <param name="preparationId">The staging identity.</param>
    /// <param name="reason">The abort reason.</param>
    /// <param name="grant">The single-use grant whose captured authorization carries the exact scope and identity of the operation.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <exception cref="ArgumentOutOfRangeException">The identity is empty or reason is undefined.</exception>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="idempotencyKey"/> is blank or <paramref name="grant"/> carries no captured authorization.</exception>
    public ArtifactStoreAbortRequest(ArtifactPreparationId preparationId, ArtifactAbortReason reason, SecurityGrant grant, IdempotencyKey idempotencyKey)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(reason);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        PreparationId = preparationId; Reason = reason; Grant = grant; IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the staging identity.</summary>
    public ArtifactPreparationId PreparationId { get; }

    /// <summary>Gets the abort reason.</summary>
    public ArtifactAbortReason Reason { get; }

    /// <summary>Gets the single-use removal grant.</summary>
    public SecurityGrant Grant { get; }

    /// <summary>Gets the exact security scope bound by the grant.</summary>
    public SecurityAuthorizationScope Scope => Grant.Scope;

    /// <summary>Gets the authenticated identity bound by the grant.</summary>
    public ExecutionIdentity Identity => Grant.Identity;

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
