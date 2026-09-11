// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains immutable correlation and ownership for one later authorized resolution.</summary>
/// <remarks>The session owner persists this evidence and any encrypted resume material before handoff. No closure, task or live scope is retained. Provider suspension remains operation-owned; external ownership never proves that the external effect stopped or completed.</remarks>
public sealed record DeferredOperationRequest
{
    /// <summary>Captures a structurally coherent deferred request without granting or consuming authority.</summary>
    /// <param name="id">The nondefault durable request identity.</param>
    /// <param name="sessionId">The nondefault owning session.</param>
    /// <param name="runId">The nondefault original run.</param>
    /// <param name="operationId">The nondefault operation retained across resolution.</param>
    /// <param name="kind">The defined deferral reason.</param>
    /// <param name="continuationOwner">The defined owner; operation-deferred work is external and provider-suspended work is runtime-owned.</param>
    /// <param name="effects">NotStarted for approval or operation deferral; Started for result pending or provider suspension.</param>
    /// <param name="operation">The nonnull normalized protected operation.</param>
    /// <param name="inputFingerprint">The nondefault normalized input fingerprint.</param>
    /// <param name="securityDecision">The nonnull reference to the preceding audited security decision.</param>
    /// <param name="createdAt">The occurrence time supplied by the owner's injected clock.</param>
    /// <param name="expiresAt">An optional exclusive expiry strictly after creation.</param>
    /// <param name="sessionVersion">The nonnegative captured session version.</param>
    /// <param name="extensions">Nonnull immutable additional classified evidence.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity or fingerprint is default, an enum is undefined, or expiry is not after creation.</exception>
    /// <exception cref="ArgumentNullException">Operation, security decision or extensions are null.</exception>
    /// <exception cref="ArgumentException">Ownership or effect-start evidence conflicts with the deferral kind.</exception>
    public DeferredOperationRequest(DeferredRequestId id, SessionId sessionId, RunId runId, OperationId operationId,
        DeferralKind kind, DeferralContinuationOwner continuationOwner, DeferralEffectState effects,
        ProtectedOperation operation, InputFingerprint inputFingerprint, SecurityDecisionReference securityDecision,
        DateTimeOffset createdAt, DateTimeOffset? expiresAt, SessionVersion sessionVersion, ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(continuationOwner);
        ArgumentOutOfRangeException.ThrowIfUndefined(effects);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentOutOfRangeException.ThrowIfEqual(inputFingerprint, default);
        ArgumentNullException.ThrowIfNull(securityDecision);
        if (expiresAt is { } expiry) { ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiry, createdAt, nameof(expiresAt)); }
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentException.ThrowIfNotEqual(effects, kind is DeferralKind.ApprovalRequired or DeferralKind.OperationDeferred
            ? DeferralEffectState.NotStarted : DeferralEffectState.Started, nameof(effects));
        if (kind == DeferralKind.OperationDeferred) { ArgumentException.ThrowIfNotEqual(continuationOwner, DeferralContinuationOwner.ExternalWorkflow, nameof(continuationOwner)); }
        if (kind == DeferralKind.ProviderSuspended) { ArgumentException.ThrowIfNotEqual(continuationOwner, DeferralContinuationOwner.RuntimeOperation, nameof(continuationOwner)); }
        Id = id; SessionId = sessionId; RunId = runId; OperationId = operationId; Kind = kind;
        ContinuationOwner = continuationOwner; Effects = effects; Operation = operation; InputFingerprint = inputFingerprint;
        SecurityDecision = securityDecision; CreatedAt = createdAt; ExpiresAt = expiresAt; SessionVersion = sessionVersion; Extensions = extensions;
    }
    /// <summary>Gets the durable request identity used by every resolution.</summary>
    /// <value>A nondefault stable identity.</value>
    public DeferredRequestId Id { get; }
    /// <summary>Gets the original owning session.</summary>
    /// <value>A nondefault identity subject to tenant-isolated lookup.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets the run that created this request.</summary>
    /// <value>A nondefault causal run; external resolution cannot reopen it after settlement.</value>
    public RunId RunId { get; }
    /// <summary>Gets the operation requiring correlated resolution.</summary>
    /// <value>A nondefault stable operation identity.</value>
    public OperationId OperationId { get; }
    /// <summary>Gets the reason work cannot currently advance.</summary>
    /// <value>A defined kind with validated ownership and start evidence.</value>
    public DeferralKind Kind { get; }
    /// <summary>Gets who owns the continuation and whether the current operation remains open.</summary>
    /// <value>RuntimeOperation requires a nonterminal wait; ExternalWorkflow permits a terminal handoff.</value>
    public DeferralContinuationOwner ContinuationOwner { get; }
    /// <summary>Gets whether deferred execution started before this request.</summary>
    /// <value>Start evidence only, never proof of effect completion or safe replay.</value>
    public DeferralEffectState Effects { get; }
    /// <summary>Gets the normalized operation requiring revalidation.</summary>
    /// <value>Nonnull descriptive evidence that grants no authority.</value>
    public ProtectedOperation Operation { get; }
    /// <summary>Gets the exact normalized input fingerprint.</summary>
    /// <value>A nondefault fingerprint; edited input requires fresh evaluation.</value>
    public InputFingerprint InputFingerprint { get; }
    /// <summary>Gets the preceding audited security decision reference.</summary>
    /// <value>A nonnull reference which the resolution owner must resolve and revalidate.</value>
    public SecurityDecisionReference SecurityDecision { get; }
    /// <summary>Gets the supplied occurrence time without reading an ambient clock.</summary>
    /// <value>The immutable original creation timestamp.</value>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>Gets the exclusive expiry when the owner configured one.</summary>
    /// <value>Null or a timestamp strictly after creation; resolution evaluates it against an injected clock.</value>
    public DateTimeOffset? ExpiresAt { get; }
    /// <summary>Gets the original session-version evidence.</summary>
    /// <value>A nonnegative revision to revalidate during resolution admission.</value>
    public SessionVersion SessionVersion { get; }
    /// <summary>Gets immutable additional classified evidence.</summary>
    /// <value>A nonnull bag requiring the owning storage and presentation policy.</value>
    public ExtensionData Extensions { get; }
    /// <summary>Gets the minimum evidence family required before the owner may advance this request.</summary>
    /// <value>Approval, terminal operation result, or original-route provider continuation evidence; none bypasses authorization.</value>
    public DeferredResumeEvidenceKind RequiredEvidence => Kind switch
    {
        DeferralKind.ApprovalRequired => DeferredResumeEvidenceKind.ApprovalResolution,
        DeferralKind.ProviderSuspended => DeferredResumeEvidenceKind.ProviderContinuation,
        DeferralKind.OperationDeferred or DeferralKind.ResultPending => DeferredResumeEvidenceKind.OperationResult,
        _ => throw new InvalidOperationException("The validated deferral kind is invalid."),
    };
}
