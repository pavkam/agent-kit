// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers the security-enforcing real network leaf.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the resolver, transport, and operation identity source additively.</summary>
        /// <param name="configure">Optional structural policy configuration.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddAgentNetwork(Action<AgentNetworkOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();
            var options = services.AddOptions<AgentNetworkOptions>()
                .Validate(static value => value.DestinationPolicy is not null, "DestinationPolicy is required.")
                .Validate(
                    static value => value.AddressResolutionLifetime > TimeSpan.Zero,
                    "AddressResolutionLifetime must be positive.")
                .Validate(
                    static value => value.MaximumResponseHeaderKilobytes > 0,
                    "MaximumResponseHeaderKilobytes must be positive.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<NetworkOperationId>, GuidNetworkOperationIdGenerator>();
            services.TryAddSingleton<INetworkNameResolver, DefaultNetworkNameResolver>();
            services.TryAddSingleton<INetworkTransport, DefaultNetworkTransport>();
            return services;
        }
    }
}
