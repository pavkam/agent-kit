// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers deterministic network boundary implementations for tests and offline hosts.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers protected scripted network services without replacing explicit host choices.</summary>
        /// <returns>The same service collection for composition chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>An <see cref="ISecurityGrantStore"/> remains required and is deliberately never fabricated.</remarks>
        public IServiceCollection AddAgentNetworkInMemory()
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<NetworkOperationId>, GuidNetworkOperationIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();
            services.TryAddSingleton<ScriptedNetworkNameResolver>(static provider => new(
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<ScriptedNetworkNameResolver>>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>()));
            services.TryAddSingleton<ScriptedNetworkTransport>(static provider => new(
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<ScriptedNetworkTransport>>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>()));
            services.TryAddSingleton<INetworkNameResolver>(static provider =>
                provider.GetRequiredService<ScriptedNetworkNameResolver>());
            services.TryAddSingleton<INetworkTransport>(static provider =>
                provider.GetRequiredService<ScriptedNetworkTransport>());
            return services;
        }
    }
}
