// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

public sealed class McpValueTypeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void McpToolName_WhenValueIsMissing_ThrowsForValue(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new McpToolName(value!));

        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData("weather.get")]
    [InlineData("weather/get-current")]
    [InlineData("Weather_Get_2")]
    [InlineData("com.example.tool")]
    public void McpToolName_WhenValueIsPresent_PreservesIdentity(string value)
    {
        var name = new McpToolName(value);

        name.Value.ShouldBe(value);
        name.ToString().ShouldBe(value);
        name.ShouldBe(new McpToolName(value));
    }

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

    [Fact]
    public void MetadataKey_WhenRead_IsNamespacedAndStable() =>
        McpMetadataKeys.ToolContractVersion.ShouldBe("com.agentkit/toolContractVersion");
}
