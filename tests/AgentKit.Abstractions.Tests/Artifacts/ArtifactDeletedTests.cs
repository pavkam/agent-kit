// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactDeleted behavior and contracts.</summary>
public sealed class ArtifactDeletedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var deleted = new ArtifactDeleted(true);
        deleted.AlreadyAbsent.ShouldBeTrue();
        deleted.ToString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new ArtifactDeleted(true);
        var copy = original with { AlreadyAbsent = false };
        copy.AlreadyAbsent.ShouldBeFalse();
        original.AlreadyAbsent.ShouldBeTrue();
    }
}
