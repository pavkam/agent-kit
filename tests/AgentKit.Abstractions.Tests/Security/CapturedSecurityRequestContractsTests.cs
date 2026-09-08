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

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("authorization");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void With_WhenCapturedScopeDiffers_ThrowsArgumentExceptionForScope(int contract)
    {
        var scope = Scope();
        var identity = Identity();
        var authorization = Authorization(scope, identity);
        var differentScope = Scope();
        Action action = contract switch
        {
            0 => () => _ = Request(scope, identity, authorization) with { Scope = differentScope },
            1 => () => _ = Grant(scope, identity, authorization) with { Scope = differentScope },
            _ => () => _ = Enforcement(scope, identity, authorization) with { Scope = differentScope },
        };

        var exception = Should.Throw<ArgumentException>(action);

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("Scope");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void With_WhenCapturedIdentityDiffers_ThrowsArgumentExceptionForIdentity(int contract)
    {
        var scope = Scope();
        var identity = Identity();
        var authorization = Authorization(scope, identity);
        var differentIdentity = Identity("other-principal");
        Action action = contract switch
        {
            0 => () => _ = Request(scope, identity, authorization) with { Identity = differentIdentity },
            1 => () => _ = Grant(scope, identity, authorization) with { Identity = differentIdentity },
            _ => () => _ = Enforcement(scope, identity, authorization) with { Identity = differentIdentity },
        };

        var exception = Should.Throw<ArgumentException>(action);

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("Identity");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void With_WhenScopeIsNull_ThrowsArgumentNullExceptionForScope(int contract)
    {
        var scope = Scope();
        var identity = Identity();
        var authorization = Authorization(scope, identity);
        Action action = contract switch
        {
            0 => () => _ = Request(scope, identity, authorization) with { Scope = null! },
            1 => () => _ = Grant(scope, identity, authorization) with { Scope = null! },
            _ => () => _ = Enforcement(scope, identity, authorization) with { Scope = null! },
        };

        var exception = Should.Throw<ArgumentNullException>(action);

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("Scope");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void With_WhenIdentityIsNull_ThrowsArgumentNullExceptionForIdentity(int contract)
    {
        var scope = Scope();
        var identity = Identity();
        var authorization = Authorization(scope, identity);
        Action action = contract switch
        {
            0 => () => _ = Request(scope, identity, authorization) with { Identity = null! },
            1 => () => _ = Grant(scope, identity, authorization) with { Identity = null! },
            _ => () => _ = Enforcement(scope, identity, authorization) with { Identity = null! },
        };

        var exception = Should.Throw<ArgumentNullException>(action);

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("Identity");
    }

    [Fact]
    public void GrantConstructor_WhenCapturedPolicyVersionDiffers_ThrowsArgumentExceptionForAuthorization()
    {
        var scope = Scope();
        var identity = Identity();
        var authorization = Authorization(scope, identity);

        var exception = Should.Throw<ArgumentException>(() =>
            Grant(scope, identity, authorization, new SecurityPolicyVersion(2)));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void GrantWith_WhenCapturedPolicyVersionDiffers_ThrowsArgumentExceptionForPolicyVersion()
    {
        var scope = Scope();
        var identity = Identity();
        var authorization = Authorization(scope, identity);

        var exception = Should.Throw<ArgumentException>(() =>
            _ = Grant(scope, identity, authorization) with { PolicyVersion = new SecurityPolicyVersion(2) });

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("PolicyVersion");
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
        (Request(scope, identity, authorization) with { Scope = scope, Identity = identity })
            .Authorization.ShouldBeSameAs(authorization);
        (Grant(scope, identity, authorization) with
        {
            Scope = scope,
            Identity = identity,
            PolicyVersion = authorization.PolicySnapshot.Version,
        }).Authorization.ShouldBeSameAs(authorization);
        (Enforcement(scope, identity, authorization) with { Scope = scope, Identity = identity })
            .Authorization.ShouldBeSameAs(authorization);
    }

    private static SecurityRequest Request(SecurityAuthorizationScope scope, ExecutionIdentity identity,
        SecurityAuthorizationContext authorization) => new(
        new SecurityRequestId(Guid.NewGuid()), scope, null, identity, authorization, new ComponentId("session"),
        SecurityOperationKind.StateRead, SecurityEffect.Observe, [Resource()], new InputFingerprint("sha256:input"),
        DateTimeOffset.UnixEpoch.AddMinutes(1));

    private static SecurityGrant Grant(SecurityAuthorizationScope scope, ExecutionIdentity identity,
        SecurityAuthorizationContext authorization, SecurityPolicyVersion? policyVersion = null) => new(
        new GrantId(Guid.NewGuid()), new SecurityRequestId(Guid.NewGuid()), scope, identity, authorization,
        new ComponentId("session"), SecurityOperationKind.StateRead, SecurityEffect.Observe, [Resource()],
        new InputFingerprint("sha256:input"), policyVersion ?? new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
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

    private static ExecutionIdentity Identity(string principal = "principal") => TestSupport.TestExecutionIdentity.Create(
        new TenantId("tenant"), new PrincipalId(principal), ExecutionSubjectKind.Human);

    private static ProtectedResource Resource() => new(ProtectedResourceKind.ApplicationState, "session:test");
}
