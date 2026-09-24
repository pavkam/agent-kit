// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers MCP client composition and reflection-driven tool factories.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers MCP client options, catalogs, and replaceable session factory hooks.</summary>
        /// <param name="configure">Optional client mechanics configuration.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Configured limits or timeouts are invalid.</exception>
        public IServiceCollection AddMcpClient(Action<McpClientOptions>? configure = null) =>
            McpClientRegistration.Add(services, configure);

        /// <summary>Registers one stdio MCP endpoint under a semantic key.</summary>
        /// <param name="key">The endpoint key.</param>
        /// <param name="configure">The endpoint configuration callback.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddMcpStdioEndpoint(
            McpEndpointKey key,
            Action<McpStdioEndpointOptions> configure) =>
            McpClientRegistration.AddStdioEndpoint(services, key, configure);

        /// <summary>Replaces one stdio MCP endpoint registration under a semantic key.</summary>
        /// <param name="key">The endpoint key.</param>
        /// <param name="configure">The endpoint configuration callback.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceMcpStdioEndpoint(
            McpEndpointKey key,
            Action<McpStdioEndpointOptions> configure) =>
            McpClientRegistration.ReplaceStdioEndpoint(services, key, configure);

        /// <summary>Registers one HTTP MCP endpoint under a semantic key.</summary>
        /// <param name="key">The endpoint key.</param>
        /// <param name="configure">The endpoint configuration callback.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddMcpHttpEndpoint(
            McpEndpointKey key,
            Action<McpHttpEndpointOptions> configure) =>
            McpClientRegistration.AddHttpEndpoint(services, key, configure);

        /// <summary>Replaces one HTTP MCP endpoint registration under a semantic key.</summary>
        /// <param name="key">The endpoint key.</param>
        /// <param name="configure">The endpoint configuration callback.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceMcpHttpEndpoint(
            McpEndpointKey key,
            Action<McpHttpEndpointOptions> configure) =>
            McpClientRegistration.ReplaceHttpEndpoint(services, key, configure);

        /// <summary>Registers one MCP client capability profile.</summary>
        /// <param name="profileId">The neutral profile id referenced by agent definitions.</param>
        /// <param name="configure">The profile configuration callback.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddMcpCapabilityProfile(
            CapabilityProfileId profileId,
            Action<McpCapabilityProfileOptions> configure) =>
            McpClientRegistration.AddCapabilityProfile(services, profileId, configure);

        /// <summary>Replaces one MCP client capability profile registration.</summary>
        /// <param name="profileId">The neutral profile id referenced by agent definitions.</param>
        /// <param name="configure">The profile configuration callback.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceMcpCapabilityProfile(
            CapabilityProfileId profileId,
            Action<McpCapabilityProfileOptions> configure) =>
            McpClientRegistration.ReplaceCapabilityProfile(services, profileId, configure);

        /// <summary>Replaces the singular MCP client session factory registration.</summary>
        /// <typeparam name="TFactory">The factory implementation type.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceMcpClientSessionFactory<TFactory>()
            where TFactory : class, IMcpClientSessionFactory =>
            McpClientRegistration.ReplaceSessionFactory<TFactory>(services);

        /// <summary>Replaces the singular MCP endpoint catalog registration.</summary>
        /// <typeparam name="TCatalog">The catalog implementation type.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceMcpEndpointCatalog<TCatalog>()
            where TCatalog : class, IMcpEndpointCatalog =>
            McpClientRegistration.ReplaceEndpointCatalog<TCatalog>(services);

        /// <summary>Replaces the singular MCP capability profile catalog registration.</summary>
        /// <typeparam name="TCatalog">The catalog implementation type.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceMcpCapabilityProfileCatalog<TCatalog>()
            where TCatalog : class, IMcpCapabilityProfileCatalog =>
            McpClientRegistration.ReplaceCapabilityProfileCatalog<TCatalog>(services);

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
