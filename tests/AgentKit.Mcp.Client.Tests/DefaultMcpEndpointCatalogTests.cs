// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

/// <summary>Verifies <see cref="DefaultMcpEndpointCatalog"/>.</summary>
public sealed class DefaultMcpEndpointCatalogTests
{
    [Fact]
    public async Task ResolveAsync_WhenEndpointIsMissing_ReturnsNotFound()
    {
        var services = new ServiceCollection();
        _ = services.AddMcpClient();
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IMcpEndpointCatalog>();
        var resolution = await catalog.ResolveAsync(new McpEndpointKey("missing"), CancellationToken.None);
        _ = resolution.ShouldBeOfType<McpEndpointNotFound>();
    }

    [Fact]
    public async Task ResolveAsync_WhenEndpointIsRegistered_ReturnsResolvedEndpoint()
    {
        var services = new ServiceCollection();
        _ = services.AddMcpClient();
        _ = services.AddMcpStdioEndpoint(new McpEndpointKey("docs"), options => options.Command = "server");
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IMcpEndpointCatalog>();
        var resolution = await catalog.ResolveAsync(new McpEndpointKey("docs"), CancellationToken.None);
        var resolved = resolution.ShouldBeOfType<McpEndpointResolved>();
        resolved.Endpoint.Key.Value.ShouldBe("docs");
    }
}
