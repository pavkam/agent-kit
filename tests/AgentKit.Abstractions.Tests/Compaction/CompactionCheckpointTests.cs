// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionCheckpoint behavior and contracts.</summary>
public sealed class CompactionCheckpointTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var checkpoint = Checkpoint();
        _ = checkpoint.Summary.ShouldHaveSingleItem();
        checkpoint.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void Constructor_WhenSummaryIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new CompactionCheckpoint(default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("summary");
    }

    [Fact]
    public void Constructor_WhenSummaryIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new CompactionCheckpoint([], ExtensionData.Empty));
        exception.ParamName.ShouldBe("summary");
    }

    [Fact]
    public void Constructor_WhenExtensionsIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new CompactionCheckpoint([new TextPart("summary", TextSemantics.Plain, ExtensionData.Empty)], null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = Checkpoint();
        var second = Checkpoint();
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Checkpoint();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static CompactionCheckpoint Checkpoint() => new([new TextPart("summary", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
}
