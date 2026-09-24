// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

/// <summary>Verifies <see cref="DefaultMcpCapabilityProfileCatalog"/>.</summary>
public sealed class DefaultMcpCapabilityProfileCatalogTests
{
    [Fact]
    public async Task ResolveAsync_WhenCapabilityIsNotMcpClient_ReturnsNotSupported()
    {
        var services = new ServiceCollection();
        _ = services.AddMcpClient();
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IMcpCapabilityProfileCatalog>();
        var resolution = await catalog.ResolveAsync(
            new AgentCapabilityReference(new CapabilityId("other"), new CapabilityProfileId("local")),
            CancellationToken.None);
        _ = resolution.ShouldBeOfType<McpCapabilityProfileNotSupported>();
    }

    [Fact]
    public async Task ResolveAsync_WhenProfileIsRegistered_ReturnsResolvedProfile()
    {
        var services = new ServiceCollection();
        _ = services.AddMcpClient();
        _ = services.AddMcpCapabilityProfile(new CapabilityProfileId("local"), options =>
        {
            options.EndpointKeys.Add(new McpEndpointKey("docs"));
        });
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IMcpCapabilityProfileCatalog>();
        var resolution = await catalog.ResolveAsync(
            new AgentCapabilityReference(McpCapabilityIds.Client, new CapabilityProfileId("local")),
            CancellationToken.None);
        var resolved = resolution.ShouldBeOfType<McpCapabilityProfileResolved>();
        _ = resolved.Profile.EndpointKeys.ShouldHaveSingleItem();
    }
}
