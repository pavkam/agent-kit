// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionProducer behavior and contracts.</summary>
public sealed class CompactionProducerTests
{
    [Fact]
    public void CompactionProducer_Equality_WhenSameValues_InstancesAreEqual() => Producer().ShouldBe(Producer());
    private static CompactionProducer Producer() => new(new CompactionStrategyKey("test"), deterministic: true, ExtensionData.Empty);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Producer();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
