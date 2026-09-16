// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionCutSelected behavior and contracts.</summary>
public sealed class CompactionCutSelectedTests
{
    [Fact]
    public void CompactionCutSelected_Equality_WhenSameValues_InstancesAreEqual() => new CompactionCutSelected(Cut()).ShouldBe(new CompactionCutSelected(Cut()));
    private static CompactionSourceRange Range() => new(new SessionSequence(1), new SessionSequence(2));
    private static CompactionCut Cut() => new(Range(), new SessionSequence(3), [new SessionEntryId(Guid.Parse("55555555-5555-5555-5555-555555555555"))]);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new CompactionCutSelected(Cut());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
