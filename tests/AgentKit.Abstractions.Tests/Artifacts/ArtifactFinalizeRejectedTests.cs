// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactFinalizeRejected behavior and contracts.</summary>
public sealed class ArtifactFinalizeRejectedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var failure = new ArtifactFailure(ArtifactFailureKind.LimitExceeded, "too big");
        var rejected = new ArtifactFinalizeRejected(failure);
        rejected.Failure.ShouldBe(failure);
        rejected.ToString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactFinalizeRejected(null!));
        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ArtifactFinalizeRejected(new ArtifactFailure(ArtifactFailureKind.LimitExceeded, "too big"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
