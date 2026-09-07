// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Composes version-aware MCP servers and reflected object-in/object-out tool classes.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the official MCP server with an explicit AgentKit version policy.</summary>
        /// <param name="versionPolicy">
        /// The protocol version policy, or null for modern-first dual-era compatibility supplied by the SDK.
        /// </param>
        /// <returns>The official MCP server builder for transport and primitive composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// Registration is additive according to the official SDK builder. The policy controls only the MCP wire
        /// revision; it does not select tool contract versions or catalog generations.
        /// </remarks>
        public IMcpServerBuilder AddAgentKitMcpServer(McpServerVersionPolicy? versionPolicy = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var policy = versionPolicy ?? McpServerVersionPolicy.Compatible;
            return services.AddMcpServer(options => options.ProtocolVersion = policy.RequiredVersion?.ToString());
        }
    }
}
