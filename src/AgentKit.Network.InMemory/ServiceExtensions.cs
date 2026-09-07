// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Dependency-injection registration for the deterministic, scripted network implementation.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="ScriptedNetworkNameResolver"/> and
        /// <see cref="ScriptedNetworkTransport"/> as the singular
        /// <see cref="INetworkNameResolver"/> and
        /// <see cref="INetworkTransport"/>.
        /// </summary>
        /// <param name="policy">
        /// The destination policy the scripted transport enforces before
        /// consulting a script; <see langword="null"/> enforces no policy.
        /// </param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Idempotent: every registration here uses <c>TryAdd</c> semantics,
        /// so calling this more than once, or alongside a real
        /// implementation that already claimed these services, keeps
        /// whichever registration happened first.
        /// </remarks>
        public IServiceCollection AddInMemoryNetwork(NetworkDestinationPolicy? policy = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<ScriptedNetworkNameResolver>();
            services.TryAddSingleton<INetworkNameResolver>(
                static provider => provider.GetRequiredService<ScriptedNetworkNameResolver>());
            services.TryAddSingleton(
                provider => new ScriptedNetworkTransport(provider.GetRequiredService<TimeProvider>(), policy));
            services.TryAddSingleton<INetworkTransport>(
                static provider => provider.GetRequiredService<ScriptedNetworkTransport>());

            return services;
        }
    }
}
