// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

/// <summary>Verifies <see cref="HistoryRepair"/> invariants and equality.</summary>
public sealed class HistoryRepairTests
{
    [Fact]
    public void Constructor_WhenSourceIdsAreDefault_Throws() =>
        Should.Throw<ArgumentException>(() => new HistoryRepair(default, HistoryRepairKind.NormalizedContent, "reason", ExtensionData.Empty)).ParamName.ShouldBe("sourceMessageIds");

    [Fact]
    public void Equals_WhenArraysContainEqualValues_IsDeepAndOrdered()
    {
        var id = new MessageId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var left = new HistoryRepair([id], HistoryRepairKind.NormalizedContent, "reason", ExtensionData.Empty);
        var right = new HistoryRepair([id], HistoryRepairKind.NormalizedContent, "reason", ExtensionData.Empty);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var id = new MessageId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var original = new HistoryRepair([id], HistoryRepairKind.NormalizedContent, "reason", ExtensionData.Empty);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
