// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers reflection-driven MCP client tool factories.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one typed MCP client factory and its immutable reflected contract.</summary>
        /// <typeparam name="TTools">The attributed class describing the expected remote tool surface.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><typeparamref name="TTools"/> has an invalid reflected contract.</exception>
        /// <remarks>Registration is idempotent and singleton because both services are immutable and thread-safe.</remarks>
        public IServiceCollection AddMcpToolClient<TTools>()
            where TTools : class
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();
            _ = services.AddMcpToolContract<TTools>();
            services.TryAddSingleton<McpToolClientFactory<TTools>>();
            return services;
        }
    }
}
