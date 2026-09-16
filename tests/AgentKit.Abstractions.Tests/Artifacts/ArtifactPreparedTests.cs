// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactPrepared behavior and contracts.</summary>
public sealed class ArtifactPreparedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var prepared = new ArtifactPrepared(PreparationId(), ArtifactId(), new ArtifactVersion("1"), DateTimeOffset.UnixEpoch);
        prepared.PreparationId.ShouldBe(PreparationId());
        prepared.ArtifactId.ShouldBe(ArtifactId());
        prepared.Version.ShouldBe(new ArtifactVersion("1"));
        prepared.ExpiresAt.ShouldBe(DateTimeOffset.UnixEpoch);
        prepared.ToString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WhenPreparationIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactPrepared(default, ArtifactId(), new ArtifactVersion("1"), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("preparationId");
    }

    [Fact]
    public void Constructor_WhenArtifactIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactPrepared(PreparationId(), default, new ArtifactVersion("1"), DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("artifactId");
    }

    [Fact]
    public void Constructor_WhenVersionIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactPrepared(PreparationId(), ArtifactId(), default, DateTimeOffset.UnixEpoch));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ArtifactPrepared(PreparationId(), ArtifactId(), new ArtifactVersion("1"), DateTimeOffset.UnixEpoch);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ArtifactPreparationId PreparationId() => new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    private static ArtifactId ArtifactId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
}
