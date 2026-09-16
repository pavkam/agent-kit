// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactPrepareRejected behavior and contracts.</summary>
public sealed class ArtifactPrepareRejectedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var failure = new ArtifactFailure(ArtifactFailureKind.Failed, "failed");
        var rejected = new ArtifactPrepareRejected(failure);
        rejected.Failure.ShouldBe(failure);
        rejected.ToString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactPrepareRejected(null!));
        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ArtifactPrepareRejected(new ArtifactFailure(ArtifactFailureKind.Failed, "failed"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
