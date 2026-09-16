// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactPrepareRequest behavior and contracts.</summary>
public sealed class ArtifactPrepareRequestTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var correlation = Correlation();
        var identity = Identity();
        var metadata = Metadata();
        var directory = new ArtifactDirectoryId("output");
        var key = new IdempotencyKey("prepare");
        using var stream = new MemoryStream();
        var request = new ArtifactPrepareRequest(AgentId(), SessionId(), null, correlation, identity, directory, metadata, stream, key);
        request.AgentId.ShouldBe(AgentId());
        request.SessionId.ShouldBe(SessionId());
        request.ToolCallId.ShouldBeNull();
        request.Correlation.ShouldBe(correlation);
        request.Identity.ShouldBe(identity);
        request.DirectoryId.ShouldBe(directory);
        request.Metadata.ShouldBe(metadata);
        request.Content.ShouldBeSameAs(stream);
        request.IdempotencyKey.ShouldBe(key);
    }

    [Fact]
    public void ArtifactPrepareRequest_WhenStreamIsUnreadable_ThrowsExactParameter()
    {
        var stream = new MemoryStream();
        stream.Dispose();
        var exception = Should.Throw<ArgumentException>(() => new ArtifactPrepareRequest(AgentId(), SessionId(), null, Correlation(), Identity(), new ArtifactDirectoryId("output"), Metadata(), stream, new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void Constructor_WhenCorrelationIsNull_ThrowsExactParameter()
    {
        using var stream = new MemoryStream();
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactPrepareRequest(AgentId(), SessionId(), null, null!, Identity(), new ArtifactDirectoryId("output"), Metadata(), stream, new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("correlation");
    }

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsExactParameter()
    {
        using var stream = new MemoryStream();
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactPrepareRequest(AgentId(), SessionId(), null, Correlation(), null!, new ArtifactDirectoryId("output"), Metadata(), stream, new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("identity");
    }

    [Fact]
    public void Constructor_WhenDirectoryIdIsBlank_ThrowsExactParameter()
    {
        using var stream = new MemoryStream();
        var exception = Should.Throw<ArgumentException>(() => new ArtifactPrepareRequest(AgentId(), SessionId(), null, Correlation(), Identity(), default, Metadata(), stream, new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("directoryId");
    }

    [Fact]
    public void Constructor_WhenMetadataIsNull_ThrowsExactParameter()
    {
        using var stream = new MemoryStream();
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactPrepareRequest(AgentId(), SessionId(), null, Correlation(), Identity(), new ArtifactDirectoryId("output"), null!, stream, new IdempotencyKey("prepare")));
        exception.ParamName.ShouldBe("metadata");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactParameter()
    {
        using var stream = new MemoryStream();
        var exception = Should.Throw<ArgumentException>(() => new ArtifactPrepareRequest(AgentId(), SessionId(), null, Correlation(), Identity(), new ArtifactDirectoryId("output"), Metadata(), stream, default));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    private static ArtifactMetadata Metadata(long declaredLength = 7) => new(new ArtifactOwnerId("session:owner"), "text/plain", declaredLength, new ContentHash("hash"), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false));
    private static AgentId AgentId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static SessionId SessionId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static InRunOperationCorrelation Correlation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        using var stream = new MemoryStream();
        var original = new ArtifactPrepareRequest(AgentId(), SessionId(), null, Correlation(), Identity(), new ArtifactDirectoryId("output"), Metadata(), stream, new IdempotencyKey("prepare"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
