// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

using static AgentKit.Abstractions.Tests.Artifacts.ArtifactContractTestData;

/// <summary>Verifies <see cref="ArtifactStoreReadOpened"/> ownership and validation.</summary>
public sealed class ArtifactStoreReadOpenedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        using var stream = new MemoryStream("content"u8.ToArray());
        var reference = Reference();
        var opened = new ArtifactStoreReadOpened(reference, stream);
        opened.Reference.ShouldBe(reference);
        opened.Content.ShouldBeSameAs(stream);
        _ = opened.ShouldBeAssignableTo<ArtifactStoreReadResult>();
    }

    [Fact]
    public void Constructor_WhenReferenceIsNull_ThrowsExactParameter()
    {
        using var stream = new MemoryStream("content"u8.ToArray());
        Should.Throw<ArgumentNullException>(() => new ArtifactStoreReadOpened(null!, stream)).ParamName.ShouldBe("reference");
    }

    [Fact]
    public void Constructor_WhenContentIsUnreadable_ThrowsExactParameter()
    {
        var stream = new MemoryStream();
        stream.Dispose();
        Should.Throw<ArgumentException>(() => new ArtifactStoreReadOpened(Reference(), stream)).ParamName.ShouldBe("content");
    }

    [Fact]
    public async Task DisposeAsync_WhenCalled_DisposesOwnedStream()
    {
        var stream = new MemoryStream("content"u8.ToArray());
        var opened = new ArtifactStoreReadOpened(Reference(), stream);
        await opened.DisposeAsync();
        stream.CanRead.ShouldBeFalse();
    }
}
