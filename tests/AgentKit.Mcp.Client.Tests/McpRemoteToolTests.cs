// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;



/// <summary>Verifies McpRemoteTool behavior and contracts.</summary>
public sealed class McpRemoteToolTests
{
    [Fact]
    public void InternalRemoteTool_WhenNameIsUninitialized_ThrowsForName()
    {
        var exception = Should.Throw<ArgumentException>(() => new McpRemoteTool(default, null));
        exception.ParamName.ShouldBe("name");
    }

    [Fact]
    public void InternalRemoteTool_WhenVersionIsUninitialized_ThrowsForVersion()
    {
        var exception = Should.Throw<ArgumentException>(() => new McpRemoteTool(new McpToolName("tool"), default(ToolVersion)));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Equality_WhenNameAndVersionMatch_TreatsInstancesAsEqual()
    {
        var first = new McpRemoteTool(new McpToolName("weather.get"), new ToolVersion("2.1"));
        var second = new McpRemoteTool(new McpToolName("weather.get"), new ToolVersion("2.1"));

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        (first with { }).ShouldBe(first);
        first.ToString().ShouldContain(nameof(McpRemoteTool));
    }
}
