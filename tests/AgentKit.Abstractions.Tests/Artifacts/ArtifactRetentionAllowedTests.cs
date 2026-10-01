// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;


/// <summary>Verifies <see cref="ArtifactRetentionAllowed"/> validation.</summary>
public sealed class ArtifactRetentionAllowedTests
{
    [Fact]
    public void Constructor_WhenCalledWithRetention_RetainsRetention()
    {
        var retention = new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false);
        var allowed = new ArtifactRetentionAllowed(retention);
        allowed.Retention.ShouldBe(retention);
        _ = allowed.ShouldBeAssignableTo<ArtifactRetentionDecision>();
    }

    [Fact]
    public void Constructor_WhenRetentionIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ArtifactRetentionAllowed(null!)).ParamName.ShouldBe("retention");
}
