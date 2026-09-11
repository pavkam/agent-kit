// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;



/// <summary>Verifies McpRemoteToolDescriptor behavior and contracts.</summary>
public sealed class McpRemoteToolDescriptorTests
{
    [Fact]
    public void RemoteToolDescriptor_WhenValuesAreValid_CapturesIdentity()
    {
        var descriptor = new McpRemoteToolDescriptor(new McpToolName("weather.get"), new ToolVersion("2.1"));
        descriptor.Name.ShouldBe(new McpToolName("weather.get"));
        descriptor.Version.ShouldBe(new ToolVersion("2.1"));
    }

    [Fact]
    public void RemoteToolDescriptor_WhenNameIsUninitialized_ThrowsForName()
    {
        var exception = Should.Throw<ArgumentException>(() => new McpRemoteToolDescriptor(default, new ToolVersion("1")));
        exception.ParamName.ShouldBe("name");
    }

    [Fact]
    public void RemoteToolDescriptor_WhenVersionIsUninitialized_ThrowsForVersion()
    {
        var exception = Should.Throw<ArgumentException>(() => new McpRemoteToolDescriptor(new McpToolName("tool"), default));
        exception.ParamName.ShouldBe("version");
    }
}
