// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;

/// <summary>Records the complete durable fact that a resolved call was accepted before invocation.</summary>
/// <remarks>This historical evidence is not a reusable grant and does not itself invoke or authorize a tool.</remarks>
public sealed record AcceptedToolCall
{
    /// <summary>Initializes a complete accepted-call record.</summary>
    /// <param name="agentId">Owning agent.</param><param name="sessionId">Owning session.</param>
    /// <param name="runId">Owning run.</param><param name="turnId">Owning turn.</param>
    /// <param name="operationId">Accepted operation.</param><param name="callId">Correlated call.</param>
    /// <param name="authorization">Historical authorization context.</param><param name="acceptance">Acceptance and grant evidence.</param>
    /// <param name="providerAlias">Exact requested provider alias.</param><param name="toolId">Resolved canonical tool.</param>
    /// <param name="toolVersion">Resolved exact version.</param><param name="effects">Captured declared effects.</param>
    /// <param name="externalIdempotencyKey">External key required by keyed idempotency, otherwise null.</param>
    /// <param name="admission">Raw admission evidence.</param><param name="normalization">Captured normalization rules.</param>
    /// <param name="projectionPolicy">Captured projection policy repeated for direct indexing.</param>
    /// <param name="requestedAt">Original request timestamp.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity or alias is default.</exception>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentException">Authorization correlation, idempotency evidence, or projection evidence is inconsistent.</exception>
    public AcceptedToolCall(
        AgentId agentId, SessionId sessionId, RunId runId, TurnId turnId, OperationId operationId, ToolCallId callId,
        SecurityAuthorizationContext authorization, ToolCallAcceptanceEvidence acceptance, ToolAlias providerAlias,
        ToolId toolId, ToolVersion toolVersion, ToolEffects effects, IdempotencyKey? externalIdempotencyKey,
        ToolCallAdmissionEvidence admission, ToolResultNormalizationSnapshot normalization,
        ToolResultProjectionPolicyReference projectionPolicy, DateTimeOffset requestedAt)
    {
        ValidateIdentities(agentId, sessionId, runId, turnId, operationId, callId);
        ArgumentNullException.ThrowIfNull(authorization);
        ValidateAuthorization(agentId, sessionId, runId, turnId, operationId, authorization);
        ArgumentNullException.ThrowIfNull(acceptance);
        ArgumentOutOfRangeException.ThrowIfEqual(providerAlias, default);
        ArgumentOutOfRangeException.ThrowIfEqual(toolId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(toolVersion, default);
        ArgumentNullException.ThrowIfNull(effects);
        ValidateIdempotency(effects, externalIdempotencyKey);
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(normalization);
        ArgumentNullException.ThrowIfNull(normalization.ExecutionPolicy, nameof(normalization));
        ArgumentNullException.ThrowIfNull(projectionPolicy);
        ArgumentException.ThrowIfNotEqual(projectionPolicy, normalization.ProjectionPolicy);

        CallId = callId; Authorization = authorization; Acceptance = acceptance; ProviderAlias = providerAlias;
        ToolId = toolId; ToolVersion = toolVersion; Effects = effects; ExternalIdempotencyKey = externalIdempotencyKey;
        Admission = admission; Normalization = normalization; ProjectionPolicy = projectionPolicy; RequestedAt = requestedAt;
    }

    /// <summary>Gets the owning agent.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound scope, which the constructor validates against the supplied identity; never a second stored copy.</value>
    public AgentId AgentId => Authorization.Scope.AgentId;
    /// <summary>Gets the owning session.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound scope, which the constructor requires to be present and equal to the supplied identity; never a second stored copy.</value>
    public SessionId SessionId => Authorization.Scope.SessionId!.Value;
    /// <summary>Gets the owning run.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound in-run correlation; never a second stored copy.</value>
    public RunId RunId => RequireCorrelation(Authorization).RunId;
    /// <summary>Gets the owning turn.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound in-run correlation; never a second stored copy.</value>
    public TurnId TurnId => RequireCorrelation(Authorization).TurnId!.Value;
    /// <summary>Gets the operation.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound in-run correlation; never a second stored copy.</value>
    public OperationId OperationId => RequireCorrelation(Authorization).OperationId;
    /// <summary>Gets the call.</summary><value>A nondefault identity.</value>
    public ToolCallId CallId { get; }
    /// <summary>Gets historical authorization.</summary><value>Exact correlated evidence, not a grant.</value>
    public SecurityAuthorizationContext Authorization { get; }
    /// <summary>Gets acceptance evidence.</summary><value>The accepted fingerprint, grant ID, and timestamp.</value>
    public ToolCallAcceptanceEvidence Acceptance { get; }
    /// <summary>Gets the requested alias.</summary><value>Exact nondefault provider text identity.</value>
    public ToolAlias ProviderAlias { get; }
    /// <summary>Gets the resolved tool.</summary><value>A nondefault canonical identity.</value>
    public ToolId ToolId { get; }
    /// <summary>Gets the resolved version.</summary><value>A nondefault exact version.</value>
    public ToolVersion ToolVersion { get; }
    /// <summary>Gets declared effects.</summary><value>Captured untrusted descriptor evidence.</value>
    public ToolEffects Effects { get; }
    /// <summary>Gets an external idempotency key.</summary><value>Present exactly for keyed idempotency.</value>
    public IdempotencyKey? ExternalIdempotencyKey { get; }
    /// <summary>Gets raw admission evidence.</summary><value>Catalog, ordinal, and raw fingerprint.</value>
    public ToolCallAdmissionEvidence Admission { get; }
    /// <summary>Gets captured normalization rules.</summary><value>Rules retained before invocation.</value>
    public ToolResultNormalizationSnapshot Normalization { get; }
    /// <summary>Gets captured projection policy.</summary><value>The same reference carried by normalization.</value>
    public ToolResultProjectionPolicyReference ProjectionPolicy { get; }
    /// <summary>Gets request time.</summary><value>The recorded timestamp without ordering inference.</value>
    public DateTimeOffset RequestedAt { get; }

    /// <summary>Validates the complete nondefault identity tuple shared by accepted and terminal records.</summary>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="sessionId">The owning session.</param>
    /// <param name="runId">The active run.</param>
    /// <param name="turnId">The active turn.</param>
    /// <param name="operationId">The operation being recorded.</param>
    /// <param name="callId">The provider-correlated tool call.</param>
    /// <exception cref="ArgumentOutOfRangeException">Any identity is default.</exception>
    internal static void ValidateIdentities(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        TurnId turnId,
        OperationId operationId,
        ToolCallId callId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(turnId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(callId, default);
    }

    /// <summary>Validates that historical authorization evidence is bound to the recorded operation.</summary>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="sessionId">The owning session.</param>
    /// <param name="runId">The active run.</param>
    /// <param name="turnId">The active turn.</param>
    /// <param name="operationId">The protected operation.</param>
    /// <param name="authorization">The nonnull evidence whose scope must match exactly.</param>
    /// <exception cref="ArgumentOutOfRangeException">Any supplied identity is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="authorization"/> is null or its correlation is not an in-run correlation.</exception>
    /// <exception cref="ArgumentException">An established scope identity does not match.</exception>
    internal static void ValidateAuthorization(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        TurnId turnId,
        OperationId operationId,
        SecurityAuthorizationContext authorization)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(turnId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, default);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfNotEqual(authorization.Scope.AgentId, agentId, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Scope.SessionId, sessionId, nameof(authorization));
        var correlation = authorization.Scope.Correlation as InRunOperationCorrelation;
        ArgumentNullException.ThrowIfNull(correlation, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(correlation.RunId, runId, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(correlation.TurnId, turnId, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(correlation.OperationId, operationId, nameof(authorization));
    }

    /// <summary>Reads the in-run correlation that <see cref="ValidateAuthorization"/> already guaranteed is present.</summary>
    /// <param name="authorization">Authorization evidence previously validated by <see cref="ValidateAuthorization"/>.</param>
    /// <returns>The exact in-run correlation bound to <paramref name="authorization"/>'s scope.</returns>
    internal static InRunOperationCorrelation RequireCorrelation(SecurityAuthorizationContext authorization)
    {
        Debug.Assert(
            authorization.Scope.Correlation is InRunOperationCorrelation,
            "ValidateAuthorization guarantees an in-run correlation before this accessor runs.");
        return (InRunOperationCorrelation) authorization.Scope.Correlation;
    }

    /// <summary>Validates exact external-key evidence against the descriptor's idempotency classification.</summary>
    /// <param name="effects">The nonnull captured effect declarations.</param>
    /// <param name="externalIdempotencyKey">The external key, required only for keyed idempotency.</param>
    /// <exception cref="ArgumentNullException"><paramref name="effects"/> is null.</exception>
    /// <exception cref="ArgumentException">Key presence does not match the captured classification.</exception>
    internal static void ValidateIdempotency(ToolEffects effects, IdempotencyKey? externalIdempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(effects);
        var keyed = effects.Idempotency is IdempotencyClassification.IdempotentWithKey;
        ArgumentException.ThrowIfNotEqual(externalIdempotencyKey.HasValue, keyed, nameof(externalIdempotencyKey));
        if (externalIdempotencyKey is { } key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(externalIdempotencyKey));
        }
    }
}
