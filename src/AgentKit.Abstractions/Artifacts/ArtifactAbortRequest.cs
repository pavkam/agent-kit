// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests idempotent removal of unpublished staging content.</summary>
public sealed record ArtifactAbortRequest
{
    /// <summary>Initializes an abort request.</summary>
    /// <param name="preparationId">The staged preparation.</param>
    /// <param name="agentId">The acting agent.</param>
    /// <param name="sessionId">The optional owning session.</param>
    /// <param name="correlation">The causal operation.</param>
    /// <param name="identity">The authenticated identity.</param>
    /// <param name="authorization">The captured authorization evidence for authority selection.</param>
    /// <param name="reason">The abort reason.</param>
    /// <param name="idempotencyKey">The caller-owned replay key.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is empty or <paramref name="reason"/> is undefined.</exception>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="idempotencyKey"/> is blank.</exception>
    public ArtifactAbortRequest(ArtifactPreparationId preparationId, AgentId agentId, SessionId? sessionId, OperationCorrelation correlation, ExecutionIdentity identity, SecurityAuthorizationContext authorization, ArtifactAbortReason reason, IdempotencyKey idempotencyKey)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfNotEqual(
            authorization.Scope, new SecurityAuthorizationScope(agentId, sessionId, correlation), nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Identity, identity, nameof(authorization));
        ArgumentOutOfRangeException.ThrowIfUndefined(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        PreparationId = preparationId; AgentId = agentId; SessionId = sessionId; Correlation = correlation;
        Identity = identity; Authorization = authorization; Reason = reason; IdempotencyKey = idempotencyKey;
    }
    /// <summary>Gets the staged preparation.</summary>
    public ArtifactPreparationId PreparationId { get; }
    /// <summary>Gets the acting agent.</summary>
    public AgentId AgentId { get; }
    /// <summary>Gets the optional owning session.</summary>
    public SessionId? SessionId { get; }
    /// <summary>Gets the causal operation.</summary>
    public OperationCorrelation Correlation { get; }
    /// <summary>Gets the authenticated identity.</summary>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets the captured authorization evidence.</summary>
    public SecurityAuthorizationContext Authorization { get; }
    /// <summary>Gets the abort reason.</summary>
    public ArtifactAbortReason Reason { get; }
    /// <summary>Gets the caller-owned replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
