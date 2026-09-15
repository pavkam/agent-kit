// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextCostEstimate"/> invariants.</summary>
public sealed class ContextCostEstimateTests
{
    [Fact]
    public void Constructor_WhenCountsAreValid_PreservesThem()
    {
        var estimate = new ContextCostEstimate(12, null);
        estimate.Utf8Bytes.ShouldBe(12);
        estimate.EstimatedTokens.ShouldBeNull();
    }

    [Theory]
    [InlineData(-1L, null, "utf8Bytes")]
    [InlineData(0L, -1, "estimatedTokens")]
    public void Constructor_WhenCountIsNegative_Throws(long bytes, int? tokens, string parameter) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ContextCostEstimate(bytes, tokens)).ParamName.ShouldBe(parameter);
}
