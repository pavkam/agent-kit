// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactDeleteRejected behavior and contracts.</summary>
public sealed class ArtifactDeleteRejectedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var failure = new ArtifactFailure(ArtifactFailureKind.NotFound, "missing");
        var rejected = new ArtifactDeleteRejected(failure);
        rejected.Failure.ShouldBe(failure);
        rejected.ToString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactDeleteRejected(null!));
        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ArtifactDeleteRejected(new ArtifactFailure(ArtifactFailureKind.NotFound, "missing"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
