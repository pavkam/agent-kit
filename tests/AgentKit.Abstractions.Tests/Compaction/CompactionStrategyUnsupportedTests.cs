// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionStrategyUnsupported behavior and contracts.</summary>
public sealed class CompactionStrategyUnsupportedTests
{
    [Fact]
    public void CompactionStrategyUnsupported_Equality_WhenSameValues_InstancesAreEqual() => new CompactionStrategyUnsupported(Rejection()).ShouldBe(new CompactionStrategyUnsupported(Rejection()));
    private static CompactionRejection Rejection() => new(CompactionRejectionKind.NoSafeCut, "no safe cut", ExtensionData.Empty);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new CompactionStrategyUnsupported(Rejection());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
