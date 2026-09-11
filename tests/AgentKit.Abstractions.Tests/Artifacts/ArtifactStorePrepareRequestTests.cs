// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactStorePrepareRequest behavior and contracts.</summary>
public sealed class ArtifactStorePrepareRequestTests
{
    [Fact]
    public void ArtifactStorePrepareRequest_WhenStagingDurationIsNotPositive_ThrowsExactParameter()
    {
        var identity = Identity();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactStorePrepareRequest(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")), PreparationId(), new ArtifactVersion("1"), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1), identity.TenantId, identity.PrincipalId, new ArtifactDirectoryId("output"), Metadata(), [1], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, Scope(), identity, Grant(identity), new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("expiresAt");
    }

    private static ArtifactMetadata Metadata(long declaredLength = 7) => new(new ArtifactOwnerId("session:owner"), "text/plain", declaredLength, new ContentHash("hash"), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false));
    private static SecurityAuthorizationScope Scope() => new(AgentId(), SessionId(), Correlation());
    private static SecurityGrant Grant(ExecutionIdentity identity) => new(new GrantId(Guid.Parse("90000000-0000-0000-0000-000000000009")), new SecurityRequestId(Guid.Parse("a0000000-0000-0000-0000-00000000000a")), Scope(), identity, new ComponentId("artifact-store"), SecurityOperationKind.Artifact, SecurityEffect.Create, [ArtifactSecurityBinding.ArtifactResource(new ArtifactId(Guid.Parse("80000000-0000-0000-0000-000000000008")))], new InputFingerprint("fingerprint"), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), 1);
    private static AgentId AgentId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static SessionId SessionId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static ArtifactPreparationId PreparationId() => new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    private static InRunOperationCorrelation Correlation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
