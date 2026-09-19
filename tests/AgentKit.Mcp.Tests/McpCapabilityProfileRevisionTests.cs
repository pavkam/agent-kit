// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="McpCapabilityProfileRevision"/>.</summary>
public sealed class McpCapabilityProfileRevisionTests
{
    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    public void Constructor_WhenValueIsNotPositive_ThrowsForValue(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new McpCapabilityProfileRevision(value));
        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void Constructor_WhenValueIsPositive_PreservesGeneration(long value)
    {
        var revision = new McpCapabilityProfileRevision(value);
        revision.Value.ShouldBe(value);
        revision.ToString().ShouldBe(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        revision.ShouldBe(new McpCapabilityProfileRevision(value));
    }
}
