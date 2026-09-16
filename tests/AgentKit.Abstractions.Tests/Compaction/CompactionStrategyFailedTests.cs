// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionStrategyFailed behavior and contracts.</summary>
public sealed class CompactionStrategyFailedTests
{
    [Fact]
    public void CompactionStrategyFailed_Constructor_WhenFailureNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new CompactionStrategyFailed(null!));
        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void CompactionStrategyFailed_Equality_WhenSameValues_InstancesAreEqual() => new CompactionStrategyFailed(Failure()).ShouldBe(new CompactionStrategyFailed(Failure()));
    private static CompactionFailure Failure() => new(CompactionFailureKind.Unknown, "unknown", retryable: false, ExtensionData.Empty);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new CompactionStrategyFailed(Failure());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
