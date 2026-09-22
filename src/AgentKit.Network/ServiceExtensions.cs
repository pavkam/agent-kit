// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>Registers the security-enforcing real network leaf.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the default unkeyed resolver and transport additively.</summary>
        /// <param name="configure">Optional structural policy configuration.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddAgentNetwork(Action<AgentNetworkOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            return services.AddAgentNetwork(new NetworkProfileKey("default"), configure);
        }

        /// <summary>Registers one keyed resolver/transport pair and profile selection metadata.</summary>
        /// <param name="key">The profile key authored by the application.</param>
        /// <param name="configure">Optional structural policy configuration.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddAgentNetwork(
            NetworkProfileKey key,
            Action<AgentNetworkOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<NetworkOperationId>, GuidNetworkOperationIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>, GuidSecurityAuditRecordIdGenerator>();

            if (key.Value == "default")
            {
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

                services.TryAddSingleton<INetworkNameResolver>(static provider => new DefaultNetworkNameResolver(
                    provider.GetRequiredService<ISecurityGrantStore>(),
                    provider.GetRequiredService<ISecurityAuditDispatcher>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<IOptions<AgentNetworkOptions>>(),
                    provider.GetService<ILogger<DefaultNetworkNameResolver>>(),
                    provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                    provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>()));
                services.TryAddSingleton<INetworkTransport>(static provider => new DefaultNetworkTransport(
                    provider.GetRequiredService<ISecurityGrantStore>(),
                    provider.GetRequiredService<ISecurityAuditDispatcher>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<IOptions<AgentNetworkOptions>>(),
                    provider.GetService<ILogger<DefaultNetworkTransport>>(),
                    provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                    provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>()));
            }

            return AgentNetworkRegistration.Add(services, key, configure);
        }
    }
}
