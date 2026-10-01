// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests reconciliation of one caller-owned reference-commit intent by preparation identity.</summary>
public sealed record ArtifactReconciliationRequest
{
    /// <summary>Initializes a reconciliation request.</summary>
    /// <param name="preparationId">The preparation whose reference-commit intent is reconciled.</param>
    /// <param name="agentId">The acting agent.</param>
    /// <param name="sessionId">The optional owning session.</param>
    /// <param name="correlation">The causal operation.</param>
    /// <param name="authorization">The captured authorization evidence; its scope must equal the agent, session, and correlation.</param>
    /// <param name="idempotencyKey">The caller-owned replay key, from which staged collection derives its per-effect keys.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="preparationId"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="idempotencyKey"/> is blank or the authorization scope differs from the supplied scope.</exception>
    public ArtifactReconciliationRequest(
        ArtifactPreparationId preparationId, AgentId agentId, SessionId? sessionId, OperationCorrelation correlation,
        SecurityAuthorizationContext authorization, IdempotencyKey idempotencyKey)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfNotEqual(
            authorization.Scope, new SecurityAuthorizationScope(agentId, sessionId, correlation), nameof(authorization));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        PreparationId = preparationId; AgentId = agentId; SessionId = sessionId; Correlation = correlation;
        Authorization = authorization; IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the preparation whose intent is reconciled.</summary>
    public ArtifactPreparationId PreparationId { get; }

    /// <summary>Gets the acting agent.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the optional owning session.</summary>
    public SessionId? SessionId { get; }

    /// <summary>Gets the causal operation.</summary>
    public OperationCorrelation Correlation { get; }

    /// <summary>Gets the captured authorization evidence.</summary>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets the caller-owned replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
