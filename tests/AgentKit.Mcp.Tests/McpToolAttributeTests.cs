// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

public sealed class McpToolAttributeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenNameIsMissing_ThrowsForName(string? name)
    {
        var exception = Should.Throw<ArgumentException>(() => new McpToolAttribute(name!, "1.0"));

        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenVersionIsMissing_ThrowsForVersion(string? version)
    {
        var exception = Should.Throw<ArgumentException>(() => new McpToolAttribute("tool", version!));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenMetadataIsValid_CapturesIdentityAndConservativeHints()
    {
        var attribute = new McpToolAttribute("weather.get", "2.1");

        attribute.Name.ShouldBe(new McpToolName("weather.get"));
        attribute.Version.ShouldBe(new ToolVersion("2.1"));
        attribute.ReadOnly.ShouldBeFalse();
        attribute.Idempotent.ShouldBeFalse();
        attribute.OpenWorld.ShouldBeTrue();
        attribute.Destructive.ShouldBeTrue();
    }

    [Fact]
    public void EffectHints_WhenSet_RoundTripIndependently()
    {
        var attribute = new McpToolAttribute("weather.get", "2.1")
        {
            ReadOnly = true,
            Idempotent = true,
            OpenWorld = false,
            Destructive = false
        };

        attribute.ReadOnly.ShouldBeTrue();
        attribute.Idempotent.ShouldBeTrue();
        attribute.OpenWorld.ShouldBeFalse();
        attribute.Destructive.ShouldBeFalse();
    }
}
