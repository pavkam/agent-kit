// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="McpHttpTransportProfile"/>.</summary>
public sealed class McpHttpTransportProfileTests
{
    [Fact]
    public void Constructor_WhenEndpointIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new McpHttpTransportProfile(null!));
        exception.ParamName.ShouldBe("endpoint");
    }

    [Theory]
    [InlineData("/relative")]
    [InlineData("ftp://mcp.example/rpc")]
    [InlineData("https://user:secret@mcp.example/rpc")]
    public void Constructor_WhenEndpointIsNotCredentialFreeHttp_ThrowsArgumentException(string value)
    {
        var uri = value.StartsWith('/')
            ? new Uri(value, UriKind.Relative)
            : new Uri(value);
        var exception = Should.Throw<ArgumentException>(() => new McpHttpTransportProfile(uri));
        exception.ParamName.ShouldBe("endpoint");
    }

    [Fact]
    public void Constructor_WhenEndpointIsAbsoluteHttps_PreservesIt()
    {
        var endpoint = new Uri("https://mcp.example/rpc");
        var profile = new McpHttpTransportProfile(endpoint);
        profile.Endpoint.ShouldBe(endpoint);
        _ = profile.ShouldBeAssignableTo<McpTransportProfile>();
    }
}
