// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionRejection behavior and contracts.</summary>
public sealed class CompactionRejectionTests
{
    [Fact]
    public void CompactionRejection_Equality_WhenSameValues_InstancesAreEqual() => Rejection().ShouldBe(Rejection());
    private static CompactionRejection Rejection() => new(CompactionRejectionKind.NoSafeCut, "no safe cut", ExtensionData.Empty);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Rejection();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
