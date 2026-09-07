// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers reflected MCP tool contracts with dependency injection.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one validated, immutable reflected tool contract.</summary>
        /// <typeparam name="TTools">The attributed class describing the MCP tool surface.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><typeparamref name="TTools"/> has an invalid reflected contract.</exception>
        /// <remarks>Registration is idempotent and keeps the first contract for the same closed tool class.</remarks>
        public IServiceCollection AddMcpToolContract<TTools>()
            where TTools : class
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton(new McpToolContract<TTools>());
            return services;
        }
    }
}
