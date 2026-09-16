// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionCheckpointProduced behavior and contracts.</summary>
public sealed class CompactionCheckpointProducedTests
{
    [Fact]
    public void CompactionCheckpointProduced_Equality_WhenSameValues_InstancesAreEqual() => new CompactionCheckpointProduced(Checkpoint(), Producer(), new CompactionSizeEstimate(1, 1, 1)).ShouldBe(new CompactionCheckpointProduced(Checkpoint(), Producer(), new CompactionSizeEstimate(1, 1, 1)));
    private static CompactionCheckpoint Checkpoint() => new([new TextPart("summary", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
    private static CompactionProducer Producer() => new(new CompactionStrategyKey("test"), deterministic: true, ExtensionData.Empty);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new CompactionCheckpointProduced(Checkpoint(), Producer(), new CompactionSizeEstimate(1, 1, 1));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
