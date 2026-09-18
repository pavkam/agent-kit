// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>Builds the representative record used to prove a configured encoding contract can persist approval evidence.</summary>
/// <remarks>
/// Because a host may replace the serializer contract entirely, initialization encodes and decodes one synthetic record that
/// exercises every shape the store writes: a pending request and a terminal response on the same line, a complete captured
/// authorization context, a tool-call correlation, a delegated approver identity with claims at two assurance levels,
/// multiple ordered resources, and every enumeration the documents carry. Failing this check at bootstrap is far safer than
/// discovering an unusable contract while committing the human decision that authorizes an effect.
/// </remarks>
internal static class JsonApprovalStoreProbe
{
    /// <summary>Creates the deterministic fidelity probe.</summary>
    /// <returns>A record carrying a fully populated approval request and its terminal response.</returns>
    /// <remarks>
    /// The value uses fixed identities and instants so the check never depends on a clock, a random source, or the host
    /// locale. Both members are populated on one record even though a real transition sets exactly one, because the probe's
    /// purpose is encoding fidelity rather than transition validity.
    /// </remarks>
    internal static JsonApprovalLogRecord Create()
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var expiresAt = createdAt.AddMinutes(30);
        var deadline = createdAt.AddHours(1);
        var scope = new SecurityAuthorizationScope(
            new AgentId(new Guid("11111111-1111-1111-1111-111111111111")),
            new SessionId(new Guid("22222222-2222-2222-2222-222222222222")),
            new InRunOperationCorrelation(
                new OperationId(new Guid("33333333-3333-3333-3333-333333333333")),
                new RunId(new Guid("44444444-4444-4444-4444-444444444444")),
                new TurnId(new Guid("55555555-5555-5555-5555-555555555555"))));
        var requester = CreateIdentity(
            new PrincipalId("probe-requester"),
            ExecutionSubjectKind.Service,
            IdentityClaimValueKind.Text,
            "probe-claim-value",
            createdAt,
            deadline,
            delegated: false);
        var approver = CreateIdentity(
            new PrincipalId("probe-approver"),
            ExecutionSubjectKind.Human,
            IdentityClaimValueKind.WholeNumber,
            "7",
            createdAt,
            deadline,
            delegated: true);
        var policyVersion = new SecurityPolicyVersion(7);
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("probe-profile"),
            new SecurityProfileVersion(2),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(new Guid("66666666-6666-6666-6666-666666666666")),
                policyVersion,
                new ContentHash("sha256:probe-policy")),
            new ComponentKey<ISecurityAuthority>("probe-authority"),
            new AgentDefinitionRevision(3),
            new ConfigurationVersion(4),
            scope,
            requester);
        ImmutableArray<ProtectedResource> resources =
        [
            new ProtectedResource(ProtectedResourceKind.Directory, "/probe/workspace"),
            new ProtectedResource(ProtectedResourceKind.File, "/probe/workspace/resource.txt"),
        ];
        var securityRequest = new SecurityRequest(
            new SecurityRequestId(new Guid("77777777-7777-7777-7777-777777777777")),
            scope,
            new ToolCallId(new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            requester,
            authorization,
            new ComponentId("probe-audience"),
            SecurityOperationKind.FileWrite,
            SecurityEffect.CreateOrReplace,
            resources,
            new InputFingerprint("sha256:probe-input"),
            deadline,
            3);
        var binding = new ApprovalScopeBinding(
            securityRequest, policyVersion, new SecurityRevocationVersion(5), createdAt, expiresAt, 2);
        var request = new ApprovalRequest(
            new ApprovalRequestId(new Guid("88888888-8888-8888-8888-888888888888")),
            binding,
            "Probe approval presentation",
            createdAt);
        var response = new ApprovalResponse(
            new ApprovalResponseId(new Guid("99999999-9999-9999-9999-999999999999")),
            request.Id,
            binding,
            ApprovalResolution.Approved,
            approver,
            createdAt.AddMinutes(1));
        return JsonApprovalLogRecord.ForCreation(request) with
        {
            Response = JsonApprovalResponse.FromDomain(response),
        };
    }

    private static ExecutionIdentity CreateIdentity(
        PrincipalId principalId,
        ExecutionSubjectKind subjectKind,
        IdentityClaimValueKind claimValueKind,
        string claimValue,
        DateTimeOffset notBefore,
        DateTimeOffset expiresAt,
        bool delegated)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(claimValue), "A bounded probe claim value is required.");
        Debug.Assert(notBefore < expiresAt, "A probe evidence window is always ordered.");
        var tenantId = new TenantId("probe-tenant");
        var issuer = new IdentityIssuerId("probe-issuer");
        var evidenceId = new AuthenticationEvidenceId("probe-evidence");
        var version = new IdentityVersion(1);
        ImmutableArray<IdentityClaim> claims =
            [new IdentityClaim(issuer, "probe-claim-type", claimValue, claimValueKind)];
        ImmutableArray<DelegationIdentityLink> delegationChain = delegated
            ?
            [
                new DelegationIdentityLink(
                    new DelegationId(new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                    tenantId,
                    new PrincipalId("probe-delegator"),
                    issuer,
                    evidenceId,
                    version,
                    notBefore,
                    [new IdentityClaim(issuer, "probe-delegated-claim", "true", IdentityClaimValueKind.Boolean)],
                    IdentityAssuranceLevel.HardwareBacked),
            ]
            : [];
        return new ExecutionIdentity(
            tenantId,
            principalId,
            subjectKind,
            new AuthenticationEvidence(
                evidenceId,
                issuer,
                "probe-method",
                notBefore,
                expiresAt,
                new AuthenticationEvidenceFingerprint(new ContentHash("sha256:probe"))),
            claims,
            delegationChain,
            IdentityAssuranceLevel.Strong,
            version);
    }
}
