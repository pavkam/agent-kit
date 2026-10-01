// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

using static AgentKit.Abstractions.Tests.Artifacts.ArtifactContractTestData;

/// <summary>Verifies <see cref="ArtifactRetentionRequest"/> validation.</summary>
public sealed class ArtifactRetentionRequestTests
{
    private static readonly ArtifactRetention _default = new(new ArtifactRetentionPolicyKey("session"), null, false);

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var metadata = Metadata();
        var request = new ArtifactRetentionRequest(metadata, _default, DateTimeOffset.UnixEpoch);
        request.Metadata.ShouldBe(metadata);
        request.ProfileDefault.ShouldBe(_default);
        request.Now.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void Constructor_WhenMetadataIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ArtifactRetentionRequest(null!, _default, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("metadata");

    [Fact]
    public void Constructor_WhenDefaultIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ArtifactRetentionRequest(Metadata(), null!, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("profileDefault");
}
