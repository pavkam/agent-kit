// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;


/// <summary>Verifies <see cref="ArtifactRetentionRejected"/> validation.</summary>
public sealed class ArtifactRetentionRejectedTests
{
    [Fact]
    public void Constructor_WhenCalledWithFailure_RetainsFailure()
    {
        var failure = new ArtifactFailure(ArtifactFailureKind.NotFound, "missing");
        var rejected = new ArtifactRetentionRejected(failure);
        rejected.Failure.ShouldBe(failure);
        _ = rejected.ShouldBeAssignableTo<ArtifactRetentionDecision>();
    }

    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ArtifactRetentionRejected(null!)).ParamName.ShouldBe("failure");
}
