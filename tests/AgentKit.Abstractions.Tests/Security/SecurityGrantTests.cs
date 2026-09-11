// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;



/// <summary>Verifies SecurityGrant behavior and contracts.</summary>
public sealed class SecurityGrantTests
{
    [Fact]
    public void Constructors_WhenCapturedEvidenceMatches_RetainExactAuthorization()
    {
        var scope = Scope();
        var identity = Identity();
        var authorization = Authorization(scope, identity);
        Grant(scope, identity, authorization).Authorization.ShouldBeSameAs(authorization);
        (Grant(scope, identity, authorization) with
        {
            Scope = scope,
            Identity = identity,
            PolicyVersion = authorization.PolicySnapshot.Version,
        }

        ).Authorization.ShouldBeSameAs(authorization);
    }

    private static SecurityGrant Grant(SecurityAuthorizationScope scope, ExecutionIdentity identity, SecurityAuthorizationContext authorization, SecurityPolicyVersion? policyVersion = null) => new(new GrantId(Guid.NewGuid()), new SecurityRequestId(Guid.NewGuid()), scope, identity, authorization, new ComponentId("session"), SecurityOperationKind.StateRead, SecurityEffect.Observe, [Resource()], new InputFingerprint("sha256:input"), policyVersion ?? new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), 1);
    private static SecurityAuthorizationContext Authorization(SecurityAuthorizationScope scope, ExecutionIdentity identity) => new(new SecurityProfileKey("default"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.NewGuid()), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1), new ConfigurationVersion(1), scope, identity);
    private static SecurityAuthorizationScope Scope() => new(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null));
    private static ExecutionIdentity Identity(string principal = "principal") => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId(principal), ExecutionSubjectKind.Human);
    private static ProtectedResource Resource() => new(ProtectedResourceKind.ApplicationState, "session:test");
}
