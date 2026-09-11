// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;



/// <summary>Verifies McpCatalogVersion behavior and contracts.</summary>
public sealed class McpCatalogVersionTests
{
    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    public void McpCatalogVersion_WhenValueIsNotPositive_ThrowsForValue(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new McpCatalogVersion(value));
        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(long.MaxValue)]
    public void McpCatalogVersion_WhenValueIsPositive_PreservesGeneration(long value)
    {
        var version = new McpCatalogVersion(value);
        version.Value.ShouldBe(value);
        version.ToString().ShouldBe(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        version.ShouldBe(new McpCatalogVersion(value));
    }
}
