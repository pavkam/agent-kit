// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers keyed MCP tool providers.</summary>
public static class McpToolSourceRegistration
{
    internal static IServiceCollection Add(
        IServiceCollection services,
        ToolSourceId sourceId,
        McpEndpointKey endpointKey,
        CapabilityProfileId capabilityProfileId)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddKeyedSingleton<IToolProvider>(
            sourceId,
            (provider, _) => new McpToolProvider(
                sourceId,
                endpointKey,
                provider.GetRequiredService<IMcpClientSessionFactory>(),
                provider.GetRequiredService<IMcpEndpointCatalog>(),
                provider.GetRequiredService<IMcpCapabilityProfileCatalog>(),
                capabilityProfileId,
                provider.GetRequiredService<IIdentifierGenerator<McpRequestId>>(),
                provider.GetRequiredService<TimeProvider>()));
        return services;
    }
}
