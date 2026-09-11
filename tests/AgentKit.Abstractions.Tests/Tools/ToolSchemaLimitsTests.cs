// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

public sealed class ToolSchemaLimitsTests
{
    [Theory]
    [InlineData(0, 1, 1, 1, "maximumUtf8Bytes")]
    [InlineData(-1, 1, 1, 1, "maximumUtf8Bytes")]
    [InlineData(1, 0, 1, 1, "maximumDepth")]
    [InlineData(1, -1, 1, 1, "maximumDepth")]
    [InlineData(1, 1, 0, 1, "maximumNodes")]
    [InlineData(1, 1, -1, 1, "maximumNodes")]
    [InlineData(1, 1, 1, 0, "maximumWork")]
    [InlineData(1, 1, 1, -1, "maximumWork")]
    public void Constructor_WhenLimitNotPositive_RejectsExactArgument(int bytes, int depth, int nodes, int work, string parameter) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolSchemaLimits(bytes, depth, nodes, work)).ParamName.ShouldBe(parameter);

    [Fact]
    public void Constructor_WhenBoundaryOrLargeLimits_RetainsAllDimensions()
    {
        var limits = new ToolSchemaLimits(1, 2, 3, int.MaxValue);
        limits.MaximumUtf8Bytes.ShouldBe(1); limits.MaximumDepth.ShouldBe(2); limits.MaximumNodes.ShouldBe(3); limits.MaximumWork.ShouldBe(int.MaxValue);
        limits.ShouldBe(new ToolSchemaLimits(1, 2, 3, int.MaxValue));
        limits.ShouldNotBe(new ToolSchemaLimits(1, 2, 3, 4));
    }
}
