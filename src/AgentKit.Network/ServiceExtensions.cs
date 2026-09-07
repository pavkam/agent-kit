// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Dependency-injection registration for the built-in, real network implementation.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="DefaultNetworkNameResolver"/> and
        /// <see cref="DefaultNetworkTransport"/> as the singular
        /// <see cref="INetworkNameResolver"/> and
        /// <see cref="INetworkTransport"/>.
        /// </summary>
        /// <param name="configure">Optional configuration for <see cref="AgentNetworkOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Idempotent: every registration here uses <c>TryAdd</c> semantics,
        /// so calling this more than once, or alongside the in-memory
        /// package that already claimed these services, keeps whichever
        /// registration happened first.
        /// </remarks>
        public IServiceCollection AddAgentNetwork(Action<AgentNetworkOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var optionsBuilder = services.AddOptions<AgentNetworkOptions>()
                .Validate(static o => o.AddressResolutionLifetime > TimeSpan.Zero, "AddressResolutionLifetime must be positive.");

            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<NetworkOperationId>>(
                static _ => new GuidIdentifierGenerator<NetworkOperationId>(static value => new NetworkOperationId(value)));
            services.TryAddSingleton<INetworkNameResolver, DefaultNetworkNameResolver>();
            services.TryAddSingleton<INetworkTransport, DefaultNetworkTransport>();

            return services;
        }
    }
}
