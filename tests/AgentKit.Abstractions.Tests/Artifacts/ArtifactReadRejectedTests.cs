// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactReadRejected behavior and contracts.</summary>
public sealed class ArtifactReadRejectedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var failure = new ArtifactFailure(ArtifactFailureKind.RetentionConflict, "held");
        var rejected = new ArtifactReadRejected(failure);
        rejected.Failure.ShouldBe(failure);
        rejected.ToString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactReadRejected(null!));
        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ArtifactReadRejected(new ArtifactFailure(ArtifactFailureKind.RetentionConflict, "held"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
