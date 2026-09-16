// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionFailure behavior and contracts.</summary>
public sealed class CompactionFailureTests
{
    [Fact]
    public void CompactionFailure_Equality_WhenSameValues_InstancesAreEqual() => Failure().ShouldBe(Failure());
    private static CompactionFailure Failure() => new(CompactionFailureKind.Unknown, "unknown", retryable: false, ExtensionData.Empty);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Failure();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
