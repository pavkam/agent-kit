// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactReadOpened behavior and contracts.</summary>
public sealed class ArtifactReadOpenedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var stream = new MemoryStream("content"u8.ToArray());
        var reference = Reference();
        var opened = new ArtifactReadOpened(reference, stream);
        opened.Reference.ShouldBe(reference);
        opened.Content.ShouldBeSameAs(stream);
    }

    [Fact]
    public void Constructor_WhenReferenceIsNull_ThrowsExactParameter()
    {
        using var stream = new MemoryStream("content"u8.ToArray());
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactReadOpened(null!, stream));
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void Constructor_WhenContentIsUnreadable_ThrowsExactParameter()
    {
        var stream = new MemoryStream();
        stream.Dispose();
        var exception = Should.Throw<ArgumentException>(() => new ArtifactReadOpened(Reference(), stream));
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public async Task ArtifactReadOpened_WhenDisposed_DisposesOwnedStream()
    {
        var stream = new MemoryStream("content"u8.ToArray());
        var opened = new ArtifactReadOpened(Reference(), stream);
        await opened.DisposeAsync();
        stream.CanRead.ShouldBeFalse();
    }

    private static ArtifactReference Reference(ArtifactId? id = null, string version = "1") => new(id ?? new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion(version), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        using var stream = new MemoryStream();
        var original = new ArtifactReadOpened(Reference(), stream);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
