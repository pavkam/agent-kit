// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

using static AgentKit.Abstractions.Tests.Artifacts.ArtifactContractTestData;

/// <summary>Verifies <see cref="ArtifactStorePrepared"/> validation.</summary>
public sealed class ArtifactStorePreparedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var prepared = new ArtifactStorePrepared(PreparationId, ArtifactId, new ArtifactVersion("1"), DateTimeOffset.UnixEpoch);
        prepared.PreparationId.ShouldBe(PreparationId);
        prepared.ArtifactId.ShouldBe(ArtifactId);
        prepared.Version.ShouldBe(new ArtifactVersion("1"));
        prepared.ExpiresAt.ShouldBe(DateTimeOffset.UnixEpoch);
        _ = prepared.ShouldBeAssignableTo<ArtifactStorePrepareResult>();
    }

    [Fact]
    public void Constructor_WhenPreparationIdIsEmpty_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactStorePrepared(default, ArtifactId, new ArtifactVersion("1"), DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("preparationId");

    [Fact]
    public void Constructor_WhenArtifactIdIsEmpty_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactStorePrepared(PreparationId, default, new ArtifactVersion("1"), DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("artifactId");

    [Fact]
    public void Constructor_WhenVersionIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ArtifactStorePrepared(PreparationId, ArtifactId, default, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("version");
}
