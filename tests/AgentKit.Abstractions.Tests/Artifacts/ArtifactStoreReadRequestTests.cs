// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactStoreReadRequest behavior and contracts.</summary>
public sealed class ArtifactStoreReadRequestTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var identity = Identity();
        var reference = Reference();
        var scope = Scope();
        var grant = Grant(identity);
        var request = new ArtifactStoreReadRequest(reference, scope, identity, grant);
        request.Reference.ShouldBe(reference);
        request.Scope.ShouldBe(scope);
        request.Identity.ShouldBe(identity);
        request.Grant.ShouldBe(grant);
        request.ToString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WhenReferenceIsNull_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStoreReadRequest(null!, Scope(), identity, Grant(identity)));
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void Constructor_WhenScopeIsNull_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStoreReadRequest(Reference(), null!, identity, Grant(identity)));
        exception.ParamName.ShouldBe("scope");
    }

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStoreReadRequest(Reference(), Scope(), null!, Grant(identity)));
        exception.ParamName.ShouldBe("identity");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactStoreReadRequest(Reference(), Scope(), identity, null!));
        exception.ParamName.ShouldBe("grant");
    }

    private static ArtifactReference Reference(ArtifactId? id = null, string version = "1") => new(id ?? new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion(version), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch);
    private static SecurityAuthorizationScope Scope() => new(AgentId(), SessionId(), Correlation());
    private static SecurityGrant Grant(ExecutionIdentity identity) => new(new GrantId(Guid.Parse("90000000-0000-0000-0000-000000000009")), new SecurityRequestId(Guid.Parse("a0000000-0000-0000-0000-00000000000a")), Scope(), identity, new ComponentId("artifact-store"), SecurityOperationKind.Artifact, SecurityEffect.Observe, [ArtifactSecurityBinding.ArtifactResource(new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")))], new InputFingerprint("fingerprint"), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), 1);
    private static AgentId AgentId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static SessionId SessionId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static InRunOperationCorrelation Correlation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var identity = Identity();
        var original = new ArtifactStoreReadRequest(Reference(), Scope(), identity, Grant(identity));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
