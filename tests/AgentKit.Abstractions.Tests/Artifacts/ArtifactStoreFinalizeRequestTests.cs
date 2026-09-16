// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactStoreFinalizeRequest behavior and contracts.</summary>
public sealed class ArtifactStoreFinalizeRequestTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var identity = Identity();
        var scope = Scope();
        var grant = Grant(identity);
        var key = new IdempotencyKey("finalize");
        var request = new ArtifactStoreFinalizeRequest(PreparationId(), scope, identity, grant, key);
        request.PreparationId.ShouldBe(PreparationId());
        request.Scope.ShouldBe(scope);
        request.Identity.ShouldBe(identity);
        request.Grant.ShouldBe(grant);
        request.IdempotencyKey.ShouldBe(key);
        request.ToString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WhenPreparationIdIsEmpty_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactStoreFinalizeRequest(default, Scope(), identity, Grant(identity), new IdempotencyKey("finalize")));
        exception.ParamName.ShouldBe("preparationId");
    }

    [Fact]
    public void Constructor_WhenScopeIsNull_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStoreFinalizeRequest(PreparationId(), null!, identity, Grant(identity), new IdempotencyKey("finalize")));
        exception.ParamName.ShouldBe("scope");
    }

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStoreFinalizeRequest(PreparationId(), Scope(), null!, Grant(identity), new IdempotencyKey("finalize")));
        exception.ParamName.ShouldBe("identity");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStoreFinalizeRequest(PreparationId(), Scope(), identity, null!, new IdempotencyKey("finalize")));
        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentException>(() => new ArtifactStoreFinalizeRequest(PreparationId(), Scope(), identity, Grant(identity), default));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    private static SecurityAuthorizationScope Scope() => new(AgentId(), SessionId(), Correlation());
    private static SecurityGrant Grant(ExecutionIdentity identity) => new(new GrantId(Guid.Parse("90000000-0000-0000-0000-000000000009")), new SecurityRequestId(Guid.Parse("a0000000-0000-0000-0000-00000000000a")), Scope(), identity, new ComponentId("artifact-store"), SecurityOperationKind.Artifact, SecurityEffect.Create, [ArtifactSecurityBinding.PreparationResource(PreparationId())], new InputFingerprint("fingerprint"), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), 1);
    private static AgentId AgentId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static SessionId SessionId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static ArtifactPreparationId PreparationId() => new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    private static InRunOperationCorrelation Correlation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var identity = Identity();
        var original = new ArtifactStoreFinalizeRequest(PreparationId(), Scope(), identity, Grant(identity), new IdempotencyKey("finalize"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
