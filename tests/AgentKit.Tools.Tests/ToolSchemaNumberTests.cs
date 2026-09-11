// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

public sealed class ToolSchemaNumberTests
{
    [Theory]
    [InlineData("-0", "0", 0)]
    [InlineData("-1", "0", -1)]
    [InlineData("1", "0", 1)]
    [InlineData("1.2", "1.20", 0)]
    [InlineData("-1.2", "-1.19", -1)]
    [InlineData("1e1000000000000000", "10e999999999999999", 0)]
    [InlineData("1e-1000000000000000", "1e-999999999999999", -1)]
    [InlineData("1.001", "1.0001", 1)]
    public void CompareTo_WhenExactDecimalsCompared_PreservesMathematicalOrder(string left, string right, int comparison)
    {
        var first = ToolSchemaNumber.Parse(ToolSchemaTestData.Instance(left)); var second = ToolSchemaNumber.Parse(ToolSchemaTestData.Instance(right));
        Math.Sign(first.CompareTo(second)).ShouldBe(comparison); Math.Sign(second.CompareTo(first)).ShouldBe(-comparison);
    }
    [Theory]
    [InlineData("true")]
    [InlineData("\"1\"")]
    [InlineData("null")]
    public void Parse_WhenValueNotNumber_RejectsExactArgument(string json) =>
        Should.Throw<ArgumentException>(() => ToolSchemaNumber.Parse(ToolSchemaTestData.Instance(json))).ParamName.ShouldBe("number");
}
