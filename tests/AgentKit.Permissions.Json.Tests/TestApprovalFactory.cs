// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

/// <summary>Creates complete deterministic approval request, binding, and response evidence for the JSON adapter cases.</summary>
/// <remarks>
/// Approval evidence is authority bearing: the binding a response carries must match the binding its request described, or
/// the store treats the decision as retargeted and refuses it. These builders therefore derive a response's binding from the
/// request by default, so a case that wants a conflict has to construct the divergence explicitly.
/// </remarks>
internal static class TestApprovalFactory
{
    /// <summary>Creates one pending approval request with replaceable identity and approver-visible text.</summary>
    /// <param name="now">The creation instant; the request deadline is five minutes later.</param>
    /// <param name="requestId">The optional approval request identity; defaults to a fixed deterministic value.</param>
    /// <param name="safePresentation">The bounded redacted approver text.</param>
    /// <param name="inputFingerprint">The security request's input fingerprint, which a case may vary to force a binding conflict.</param>
    /// <param name="identity">The optional requester identity; defaults to a minimal test identity.</param>
    /// <returns>A valid pending request the JSON store accepts.</returns>
    internal static ApprovalRequest CreateRequest(
        DateTimeOffset now,
        ApprovalRequestId? requestId = null,
        string safePresentation = "Write workspace file",
        string inputFingerprint = "sha256:input",
        ExecutionIdentity? identity = null) => new(
            requestId ?? new ApprovalRequestId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            CreateBinding(now, inputFingerprint, identity),
            safePresentation,
            now);

    /// <summary>Creates the exact grant-relevant scope an approver decides about.</summary>
    /// <param name="now">The first permitted grant instant; the binding expires five minutes later.</param>
    /// <param name="inputFingerprint">The security request's input fingerprint.</param>
    /// <param name="identity">The optional requester identity; defaults to a minimal test identity.</param>
    /// <returns>A binding whose approved use count and expiry stay inside the request that justified them.</returns>
    internal static ApprovalScopeBinding CreateBinding(
        DateTimeOffset now,
        string inputFingerprint = "sha256:input",
        ExecutionIdentity? identity = null)
    {
        var requester = identity ?? TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("requester"), ExecutionSubjectKind.Human);
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            null,
            new BeforeRunOperationCorrelation(
                new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000002")), null));
        var securityRequest = new SecurityRequest(
            new SecurityRequestId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
            scope,
            null,
            requester,
            new ComponentId("test"),
            SecurityOperationKind.FileWrite,
            SecurityEffect.CreateOrReplace,
            [new ProtectedResource(ProtectedResourceKind.File, "/workspace/file.txt")],
            new InputFingerprint(inputFingerprint),
            now.AddMinutes(5));
        return new ApprovalScopeBinding(
            securityRequest,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            now,
            now.AddMinutes(5),
            1);
    }

    /// <summary>Creates the terminal decision that resolves one pending request.</summary>
    /// <param name="request">The request being decided; its binding is reused so the decision is not retargeted.</param>
    /// <param name="resolution">The terminal decision recorded by the approver.</param>
    /// <param name="responseId">The optional response identity; defaults to a fixed deterministic value.</param>
    /// <returns>A response the JSON store accepts for <paramref name="request"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    internal static ApprovalResponse CreateResponse(
        ApprovalRequest request,
        ApprovalResolution resolution = ApprovalResolution.Approved,
        ApprovalResponseId? responseId = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ApprovalResponse(
            responseId ?? new ApprovalResponseId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            request.Id,
            request.Binding,
            resolution,
            TestExecutionIdentity.Create(
                new TenantId("tenant"), new PrincipalId("approver"), ExecutionSubjectKind.Human),
            request.CreatedAt.AddSeconds(1));
    }

    /// <summary>Creates a delegated approver identity carrying claims and a delegation chain.</summary>
    /// <param name="now">The authentication instant used to derive a later expiry.</param>
    /// <returns>An identity that exercises every optional branch of the persisted approver mirror.</returns>
    internal static ExecutionIdentity CreateApproverIdentity(DateTimeOffset now) =>
        TestGrantFactory.CreateRichIdentity(now);
}
