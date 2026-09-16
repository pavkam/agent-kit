// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies NoSafeCompactionCut behavior and contracts.</summary>
public sealed class NoSafeCompactionCutTests
{
    [Fact]
    public void NoSafeCompactionCut_Equality_WhenSameValues_InstancesAreEqual() => new NoSafeCompactionCut(Rejection()).ShouldBe(new NoSafeCompactionCut(Rejection()));
    private static CompactionRejection Rejection() => new(CompactionRejectionKind.NoSafeCut, "no safe cut", ExtensionData.Empty);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NoSafeCompactionCut(Rejection());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
