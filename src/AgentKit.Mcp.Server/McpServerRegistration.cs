// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers keyed MCP server hosts.</summary>
internal static class McpServerRegistration
{
    internal static IServiceCollection Add(
        IServiceCollection services,
        McpServerKey key,
        Action<McpServerListenerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new McpServerListenerOptions();
        configure(options);
        if (options.Transport is null)
        {
            throw new InvalidOperationException("McpServerListenerOptions.Transport must be configured.");
        }

        _ = services.AddKeyedSingleton(key.Value, new McpServerEndpoint(key, options.Transport));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMcpPrimitiveHandler, AgentKitPrimitiveHandler>(
            static provider => new AgentKitPrimitiveHandler(provider)));
        services.TryAddSingleton<IMcpServer, McpServerHost>();
        return services;
    }
}
