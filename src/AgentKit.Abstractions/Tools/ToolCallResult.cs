// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records the complete authoritative terminal evidence for one bounded tool call.</summary>
/// <remarks>Unknown numeric statuses remain exact. Construction validates local structure but neither reauthorizes nor measures canonical aggregate bytes.</remarks>
public sealed record ToolCallResult
{
    /// <summary>Initializes one authoritative terminal record.</summary>
    /// <param name="agentId">Owning agent.</param><param name="sessionId">Owning session.</param><param name="runId">Owning run.</param><param name="turnId">Owning turn.</param>
    /// <param name="operationId">Operation identity.</param><param name="callId">Call identity.</param><param name="authorization">Historical authorization evidence.</param>
    /// <param name="grantId">A historically issued grant identity, if any.</param><param name="acceptance">Accepted invocation evidence, if invocation was accepted.</param>
    /// <param name="providerAlias">Exact requested alias.</param><param name="toolId">Resolved canonical tool, if resolved.</param><param name="toolVersion">Resolved exact version, if resolved.</param>
    /// <param name="effects">Captured effects when resolved.</param><param name="externalIdempotencyKey">Optional external keyed-idempotency evidence when extraction reached that stage.</param><param name="admission">Raw admission evidence.</param>
    /// <param name="status">Exact numeric terminal status.</param><param name="content">Initialized normalized content within the captured part bound.</param><param name="error">Optional safe error evidence.</param>
    /// <param name="sideEffectCertainty">Defined side-effect evidence other than <see cref="SideEffectCertainty.NotApplicable"/>.</param><param name="usage">Optional reported usage.</param><param name="retryable">Retained advice that a future request may be retried under policy; never permission to replay this terminally recorded effect.</param>
    /// <param name="normalization">Captured normalization rules.</param><param name="normalizationInfo">Actual normalization evidence.</param><param name="projectionPolicy">Captured projection policy repeated for indexing.</param>
    /// <param name="requestedAt">Request timestamp.</param><param name="invocationStartedAt">Invocation start timestamp when accepted.</param><param name="completedAt">Terminal timestamp.</param><param name="extensions">Compatible immutable evidence.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity, alias, or certainty value is invalid.</exception>
    /// <exception cref="ArgumentNullException">A required reference or content item is null.</exception>
    /// <exception cref="ArgumentException">Resolved, acceptance, success, retry, content, idempotency, authorization, or policy evidence is inconsistent.</exception>
    public ToolCallResult(
        AgentId agentId, SessionId sessionId, RunId runId, TurnId turnId, OperationId operationId, ToolCallId callId,
        SecurityAuthorizationContext authorization, GrantId? grantId, ToolCallAcceptanceEvidence? acceptance,
        ToolAlias providerAlias, ToolId? toolId, ToolVersion? toolVersion, ToolEffects? effects,
        IdempotencyKey? externalIdempotencyKey, ToolCallAdmissionEvidence admission, ToolTerminalStatus status,
        ImmutableArray<ToolResultContent> content, ToolError? error, SideEffectCertainty sideEffectCertainty,
        ToolUsage? usage, bool retryable, ToolResultNormalizationSnapshot normalization,
        ToolResultNormalizationInfo normalizationInfo, ToolResultProjectionPolicyReference projectionPolicy,
        DateTimeOffset requestedAt, DateTimeOffset? invocationStartedAt, DateTimeOffset completedAt,
        ExtensionData extensions)
    {
        AcceptedToolCall.ValidateIdentities(agentId, sessionId, runId, turnId, operationId, callId);
        ArgumentNullException.ThrowIfNull(authorization);
        AcceptedToolCall.ValidateAuthorization(agentId, sessionId, runId, turnId, operationId, authorization);
        if (grantId is { } retainedGrantId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(retainedGrantId, default, nameof(grantId));
        }
        ArgumentOutOfRangeException.ThrowIfEqual(providerAlias, default);
        ArgumentException.ThrowIfNotEqual(toolId.HasValue, toolVersion.HasValue, nameof(toolVersion));
        if (toolId is { } resolvedToolId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(resolvedToolId, default, nameof(toolId));
        }
        if (toolVersion is { } resolvedToolVersion)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(resolvedToolVersion, default, nameof(toolVersion));
        }
        ArgumentException.ThrowIfNotEqual(effects is not null, toolId.HasValue, nameof(effects));
        if (effects is not null)
        {
            if (externalIdempotencyKey is { } key)
            {
                ArgumentException.ThrowIfNotEqual(
                    effects.Idempotency,
                    IdempotencyClassification.IdempotentWithKey,
                    nameof(externalIdempotencyKey));
                ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(externalIdempotencyKey));
            }
        }
        else
        {
            ArgumentException.ThrowIfNotEqual(externalIdempotencyKey, null, nameof(externalIdempotencyKey));
        }

        ArgumentNullException.ThrowIfNull(admission);
        ArgumentException.ThrowIfContainsNull(content);
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        ArgumentOutOfRangeException.ThrowIfEqual(
            sideEffectCertainty,
            SideEffectCertainty.NotApplicable,
            nameof(sideEffectCertainty));
        ArgumentNullException.ThrowIfNull(normalization);
        ArgumentException.ThrowIfNotEqual(normalization.ExecutionPolicy is not null, toolId.HasValue, nameof(normalization));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(content.Length, normalization.Bounds.MaximumParts, nameof(content));
        ArgumentNullException.ThrowIfNull(normalizationInfo);
        ArgumentNullException.ThrowIfNull(projectionPolicy);
        ArgumentException.ThrowIfNotEqual(projectionPolicy, normalization.ProjectionPolicy);
        ArgumentNullException.ThrowIfNull(extensions);

        if (acceptance is null)
        {
            ArgumentException.ThrowIfNotEqual(invocationStartedAt, null, nameof(invocationStartedAt));
        }
        else
        {
            ArgumentException.ThrowIfNotEqual(toolId.HasValue, true, nameof(toolId));
            ArgumentException.ThrowIfNotEqual(grantId, acceptance.InvocationGrantId, nameof(grantId));
        }

        if (status is ToolTerminalStatus.Succeeded)
        {
            ArgumentException.ThrowIfNotEqual(toolId.HasValue, true, nameof(toolId));
            ArgumentNullException.ThrowIfNull(acceptance);
            ArgumentException.ThrowIfNotEqual(invocationStartedAt.HasValue, true, nameof(invocationStartedAt));
            ArgumentException.ThrowIfNotEqual(error, null, nameof(error));
        }

        var possiblyStarted = invocationStartedAt.HasValue
            || sideEffectCertainty is not SideEffectCertainty.DefinitelyNotPerformed;
        if (retryable && possiblyStarted && effects?.Effect is ToolEffect.Mutating)
        {
            var safe = effects.Idempotency is IdempotencyClassification.Idempotent
                || (effects.Idempotency is IdempotencyClassification.IdempotentWithKey
                    && externalIdempotencyKey.HasValue);
            ArgumentException.ThrowIfNotEqual(safe, true, nameof(retryable));
        }

        CallId = callId; Authorization = authorization; GrantId = grantId; Acceptance = acceptance;
        ProviderAlias = providerAlias; ToolId = toolId; ToolVersion = toolVersion; Effects = effects;
        ExternalIdempotencyKey = externalIdempotencyKey; Admission = admission; Status = status; Content = content;
        Error = error; SideEffectCertainty = sideEffectCertainty; Usage = usage; Retryable = retryable;
        Normalization = normalization; NormalizationInfo = normalizationInfo; ProjectionPolicy = projectionPolicy;
        RequestedAt = requestedAt; InvocationStartedAt = invocationStartedAt; CompletedAt = completedAt; Extensions = extensions;
    }

    /// <summary>Gets the owning agent.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound scope, which the constructor validates against the supplied identity; never a second stored copy.</value>
    public AgentId AgentId => Authorization.Scope.AgentId;
    /// <summary>Gets the owning session.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound scope, which the constructor requires to be present and equal to the supplied identity; never a second stored copy.</value>
    public SessionId SessionId => Authorization.Scope.SessionId!.Value;
    /// <summary>Gets the owning run.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound in-run correlation; never a second stored copy.</value>
    public RunId RunId => AcceptedToolCall.RequireCorrelation(Authorization).RunId;
    /// <summary>Gets the owning turn.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound in-run correlation; never a second stored copy.</value>
    public TurnId TurnId => AcceptedToolCall.RequireCorrelation(Authorization).TurnId!.Value;
    /// <summary>Gets the operation.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound in-run correlation; never a second stored copy.</value>
    public OperationId OperationId => AcceptedToolCall.RequireCorrelation(Authorization).OperationId;
    /// <summary>Gets the call.</summary><value>A nondefault identity.</value>
    public ToolCallId CallId { get; }
    /// <summary>Gets historical authorization evidence.</summary><value>Exact correlation that grants no current authority.</value>
    public SecurityAuthorizationContext Authorization { get; }
    /// <summary>Gets an issued grant identity.</summary><value>Historical correlation or null.</value>
    public GrantId? GrantId { get; }
    /// <summary>Gets accepted invocation evidence.</summary><value>Null when no invocation was accepted.</value>
    public ToolCallAcceptanceEvidence? Acceptance { get; }
    /// <summary>Gets the requested alias.</summary><value>Exact nondefault provider alias.</value>
    public ToolAlias ProviderAlias { get; }
    /// <summary>Gets the resolved tool.</summary><value>Null exactly when resolution failed.</value>
    public ToolId? ToolId { get; }
    /// <summary>Gets the resolved version.</summary><value>Paired with <see cref="ToolId"/>.</value>
    public ToolVersion? ToolVersion { get; }
    /// <summary>Gets captured effects.</summary><value>Present exactly for a resolved descriptor.</value>
    public ToolEffects? Effects { get; }
    /// <summary>Gets external idempotency evidence.</summary><value>Present only for keyed idempotency; it may be absent when rejection preceded key extraction.</value>
    public IdempotencyKey? ExternalIdempotencyKey { get; }
    /// <summary>Gets raw admission evidence.</summary><value>Evidence retained for every terminal path.</value>
    public ToolCallAdmissionEvidence Admission { get; }
    /// <summary>Gets exact terminal status.</summary><value>Known or future numeric evidence retained without coercion.</value>
    public ToolTerminalStatus Status { get; }
    /// <summary>Gets normalized content.</summary><value>Initialized, nonnull items within the captured part bound.</value>
    public ImmutableArray<ToolResultContent> Content { get; }
    /// <summary>Gets safe error evidence.</summary><value>Null when none was safely retained and always null for success.</value>
    public ToolError? Error { get; }
    /// <summary>Gets side-effect certainty.</summary><value>A defined evidence state.</value>
    public SideEffectCertainty SideEffectCertainty { get; }
    /// <summary>Gets optional usage.</summary><value>Null means unreported, not zero.</value>
    public ToolUsage? Usage { get; }
    /// <summary>Gets the retained retry advice.</summary><value>True when a future request may be retried under policy; this never permits replaying the terminally recorded effect, whose runtime retry still checks identity, idempotency, deadline, and budgets.</value>
    public bool Retryable { get; }
    /// <summary>Gets captured normalization rules.</summary><value>Immutable admission-time policy evidence.</value>
    public ToolResultNormalizationSnapshot Normalization { get; }
    /// <summary>Gets actual normalization evidence.</summary><value>Measured and applied transformations without invented zeroes.</value>
    public ToolResultNormalizationInfo NormalizationInfo { get; }
    /// <summary>Gets captured projection policy.</summary><value>The same reference carried by normalization.</value>
    public ToolResultProjectionPolicyReference ProjectionPolicy { get; }
    /// <summary>Gets request time.</summary><value>A timestamp fact without ordering inference.</value>
    public DateTimeOffset RequestedAt { get; }
    /// <summary>Gets invocation start time.</summary><value>Present only with acceptance evidence.</value>
    public DateTimeOffset? InvocationStartedAt { get; }
    /// <summary>Gets terminal time.</summary><value>A timestamp fact without ordering inference.</value>
    public DateTimeOffset CompletedAt { get; }
    /// <summary>Gets compatible terminal evidence.</summary><value>A nonnull immutable bag.</value>
    public ExtensionData Extensions { get; }

    /// <summary>Determines complete structural terminal-record equality.</summary>
    /// <param name="other">The record to compare, or null.</param>
    /// <returns>True when every scalar/reference field and ordered content item is equal.</returns>
    public bool Equals(ToolCallResult? other) => other is not null &&
        AgentId == other.AgentId && SessionId == other.SessionId && RunId == other.RunId && TurnId == other.TurnId &&
        OperationId == other.OperationId && CallId == other.CallId && Authorization == other.Authorization &&
        GrantId == other.GrantId && Acceptance == other.Acceptance && ProviderAlias == other.ProviderAlias &&
        ToolId == other.ToolId && ToolVersion == other.ToolVersion && Effects == other.Effects &&
        ExternalIdempotencyKey == other.ExternalIdempotencyKey && Admission == other.Admission && Status == other.Status &&
        Content.SequenceEqual(other.Content) && Error == other.Error && SideEffectCertainty == other.SideEffectCertainty &&
        Usage == other.Usage && Retryable == other.Retryable && Normalization == other.Normalization &&
        NormalizationInfo == other.NormalizationInfo && ProjectionPolicy == other.ProjectionPolicy &&
        RequestedAt == other.RequestedAt && InvocationStartedAt == other.InvocationStartedAt &&
        CompletedAt == other.CompletedAt && Extensions == other.Extensions;

    /// <summary>Returns a hash compatible with complete structural equality.</summary>
    /// <returns>A hash over every scalar/reference field and ordered content item.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(AgentId); hash.Add(SessionId); hash.Add(RunId); hash.Add(TurnId); hash.Add(OperationId); hash.Add(CallId);
        hash.Add(Authorization); hash.Add(GrantId); hash.Add(Acceptance); hash.Add(ProviderAlias); hash.Add(ToolId);
        hash.Add(ToolVersion); hash.Add(Effects); hash.Add(ExternalIdempotencyKey); hash.Add(Admission); hash.Add(Status);
        foreach (var item in Content)
        {
            hash.Add(item);
        }
        hash.Add(Error); hash.Add(SideEffectCertainty); hash.Add(Usage); hash.Add(Retryable); hash.Add(Normalization);
        hash.Add(NormalizationInfo); hash.Add(ProjectionPolicy); hash.Add(RequestedAt); hash.Add(InvocationStartedAt);
        hash.Add(CompletedAt); hash.Add(Extensions);
        return hash.ToHashCode();
    }
}
