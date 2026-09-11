// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactDeleteRequest behavior and contracts.</summary>
public sealed class ArtifactDeleteRequestTests
{
    [Fact]
    public void ArtifactDeleteRequest_WhenIdempotencyKeyIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactDeleteRequest(AgentId(), SessionId(), null, Correlation(), Identity(), Reference(), default));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    private static ArtifactReference Reference(ArtifactId? id = null, string version = "1") => new(id ?? new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion(version), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch);
    private static AgentId AgentId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static SessionId SessionId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static InRunOperationCorrelation Correlation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
