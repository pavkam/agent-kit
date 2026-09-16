// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactRetention behavior and contracts.</summary>
public sealed class ArtifactRetentionTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var retention = new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), DateTimeOffset.UnixEpoch, true);
        retention.Policy.ShouldBe(new ArtifactRetentionPolicyKey("session"));
        retention.ExpiresAt.ShouldBe(DateTimeOffset.UnixEpoch);
        retention.LegalHold.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenPolicyIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactRetention(default, null, false));
        exception.ParamName.ShouldBe("policy");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
