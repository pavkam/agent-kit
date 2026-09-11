// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactPrepareRequest behavior and contracts.</summary>
public sealed class ArtifactPrepareRequestTests
{
    [Fact]
    public void ArtifactPrepareRequest_WhenStreamIsUnreadable_ThrowsExactParameter()
    {
        var stream = new MemoryStream();
        stream.Dispose();
        var exception = Should.Throw<ArgumentException>(() => new ArtifactPrepareRequest(AgentId(), SessionId(), null, Correlation(), Identity(), new ArtifactDirectoryId("output"), Metadata(), stream, new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("content");
    }

    private static ArtifactMetadata Metadata(long declaredLength = 7) => new(new ArtifactOwnerId("session:owner"), "text/plain", declaredLength, new ContentHash("hash"), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false));
    private static AgentId AgentId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static SessionId SessionId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static InRunOperationCorrelation Correlation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
