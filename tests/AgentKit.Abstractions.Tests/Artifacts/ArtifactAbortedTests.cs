// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactAborted behavior and contracts.</summary>
public sealed class ArtifactAbortedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var aborted = new ArtifactAborted(true);
        aborted.AlreadyAbsent.ShouldBeTrue();
        aborted.ToString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new ArtifactAborted(false);
        var second = new ArtifactAborted(false);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenComparedThroughBaseType_UsesValueEquality()
    {
        ArtifactAbortResult first = new ArtifactAborted(true);
        ArtifactAbortResult second = new ArtifactAborted(true);
        first.Equals(second).ShouldBeTrue();
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new ArtifactAborted(true);
        var copy = original with { AlreadyAbsent = false };
        copy.AlreadyAbsent.ShouldBeFalse();
        original.AlreadyAbsent.ShouldBeTrue();
    }
}
