// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="McpStdioTransportProfile"/>.</summary>
public sealed class McpStdioTransportProfileTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenCommandIsMissing_ThrowsArgumentException(string? command)
    {
        var exception = Should.Throw<ArgumentException>(() => new McpStdioTransportProfile(command!, []));
        exception.ParamName.ShouldBe("command");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new McpStdioTransportProfile("server", default));
        exception.ParamName.ShouldBe("arguments");
    }

    [Fact]
    public void Constructor_WhenArgumentsContainNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new McpStdioTransportProfile("server", ["ok", null!]));
        exception.ParamName.ShouldBe("arguments");
    }

    [Fact]
    public void Equals_WhenArgumentsAreSeparateArrays_ComparesTheSequence()
    {
        var first = new McpStdioTransportProfile("server", ["--a", "--b"]);
        var second = new McpStdioTransportProfile("server", ["--a", "--b"]);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ShouldNotBe(new McpStdioTransportProfile("server", ["--b", "--a"]));
        _ = first.ShouldBeAssignableTo<McpTransportProfile>();
    }
}
