// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactIntegrity behavior and contracts.</summary>
public sealed class ArtifactIntegrityTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var integrity = new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch);
        integrity.ContentHash.ShouldBe(new ContentHash("hash"));
        integrity.VerifiedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void Constructor_WhenContentHashIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactIntegrity(default, DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("contentHash");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
