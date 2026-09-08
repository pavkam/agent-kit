// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

public sealed class CapturedSecurityRequestContractsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenCapturedScopeDiffers_ThrowsArgumentExceptionForAuthorization(int contract)
    {
        var scope = Scope();
        var identity = Identity();
        var authorization = Authorization(new SecurityAuthorizationScope(
            scope.AgentId, scope.SessionId,
            new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null)), identity);

        var exception = contract switch
        {
            0 => Should.Throw<ArgumentException>(() => Request(scope, identity, authorization)),
            1 => Should.Throw<ArgumentException>(() => Grant(scope, identity, authorization)),
            _ => Should.Throw<ArgumentException>(() => Enforcement(scope, identity, authorization)),
        };

        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructors_WhenCapturedEvidenceMatches_RetainExactAuthorization()
    {
        var scope = Scope();
        var identity = Identity();
        var authorization = Authorization(scope, identity);

        Request(scope, identity, authorization).Authorization.ShouldBeSameAs(authorization);
        Grant(scope, identity, authorization).Authorization.ShouldBeSameAs(authorization);
        Enforcement(scope, identity, authorization).Authorization.ShouldBeSameAs(authorization);
    }

    private static SecurityRequest Request(SecurityAuthorizationScope scope, ExecutionIdentity identity,
        SecurityAuthorizationContext authorization) => new(
        new SecurityRequestId(Guid.NewGuid()), scope, null, identity, authorization, new ComponentId("session"),
        SecurityOperationKind.StateRead, SecurityEffect.Observe, [Resource()], new InputFingerprint("sha256:input"),
        DateTimeOffset.UnixEpoch.AddMinutes(1));

    private static SecurityGrant Grant(SecurityAuthorizationScope scope, ExecutionIdentity identity,
        SecurityAuthorizationContext authorization) => new(
        new GrantId(Guid.NewGuid()), new SecurityRequestId(Guid.NewGuid()), scope, identity, authorization,
        new ComponentId("session"), SecurityOperationKind.StateRead, SecurityEffect.Observe, [Resource()],
        new InputFingerprint("sha256:input"), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), 1);

    private static SecurityEnforcementRequest Enforcement(SecurityAuthorizationScope scope, ExecutionIdentity identity,
        SecurityAuthorizationContext authorization) => new(
        scope, identity, authorization, new ComponentId("session"), SecurityOperationKind.StateRead,
        SecurityEffect.Observe, [Resource()], new InputFingerprint("sha256:input"), new SecurityRevocationVersion(1));

    private static SecurityAuthorizationContext Authorization(
        SecurityAuthorizationScope scope, ExecutionIdentity identity) => new(
        new SecurityProfileKey("default"), new SecurityProfileVersion(1),
        new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.NewGuid()),
            new SecurityPolicyVersion(1), new ContentHash("sha256:policy")),
        new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1),
        new ConfigurationVersion(1), scope, identity);

    private static SecurityAuthorizationScope Scope() => new(
        new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()),
        new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null));

    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(
        new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    private static ProtectedResource Resource() => new(ProtectedResourceKind.ApplicationState, "session:test");
}
