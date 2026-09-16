// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactFailure behavior and contracts.</summary>
public sealed class ArtifactFailureTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var failure = new ArtifactFailure(ArtifactFailureKind.NotFound, "not found");
        failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        failure.SafeMessage.ShouldBe("not found");
        failure.ToString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactFailure((ArtifactFailureKind) 999, "message"));
        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactFailure(ArtifactFailureKind.Denied, " "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new ArtifactFailure(ArtifactFailureKind.Conflict, "conflict");
        var second = new ArtifactFailure(ArtifactFailureKind.Conflict, "conflict");
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ArtifactFailure(ArtifactFailureKind.Unavailable, "down");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
