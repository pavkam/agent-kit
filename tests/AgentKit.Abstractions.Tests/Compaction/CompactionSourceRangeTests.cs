// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionSourceRange behavior and contracts.</summary>
public sealed class CompactionSourceRangeTests
{
    [Fact]
    public void CompactionSourceRange_Constructor_WhenEndPrecedesStart_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new CompactionSourceRange(new SessionSequence(5), new SessionSequence(1)));
        exception.ParamName.ShouldBe("endInclusive");
    }

    [Fact]
    public void CompactionSourceRange_Equality_WhenSameValues_InstancesAreEqual() => new CompactionSourceRange(new SessionSequence(1), new SessionSequence(2)).ShouldBe(new CompactionSourceRange(new SessionSequence(1), new SessionSequence(2)));

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new CompactionSourceRange(new SessionSequence(1), new SessionSequence(2));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
