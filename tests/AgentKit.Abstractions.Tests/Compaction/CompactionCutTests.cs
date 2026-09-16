// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionCut behavior and contracts.</summary>
public sealed class CompactionCutTests
{
    [Fact]
    public void CompactionCut_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = Cut();
        var second = Cut();
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void CompactionCut_Equality_WhenCoveredEntryIdsDiffer_InstancesAreNotEqual()
    {
        var first = Cut();
        var second = new CompactionCut(Range(), new SessionSequence(3), [new SessionEntryId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))]);
        first.ShouldNotBe(second);
    }

    private static CompactionSourceRange Range() => new(new SessionSequence(1), new SessionSequence(2));
    private static CompactionCut Cut() => new(Range(), new SessionSequence(3), [new SessionEntryId(Guid.Parse("55555555-5555-5555-5555-555555555555"))]);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Cut();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
