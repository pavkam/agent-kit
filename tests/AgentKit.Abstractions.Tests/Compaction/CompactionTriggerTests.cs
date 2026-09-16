// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionTrigger behavior and contracts.</summary>
public sealed class CompactionTriggerTests
{
    [Fact]
    public void CompactionTrigger_Equality_WhenSameValues_InstancesAreEqual() => Trigger().ShouldBe(Trigger());
    private static CompactionTrigger Trigger() => new(CompactionTriggerKind.ExplicitMaintenance, "test", null);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Trigger();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
