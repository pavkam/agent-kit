// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory;

/// <summary>Registers deterministic process-local security-grant storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the process-local store when no security-grant store implementation was already selected.</summary>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// Registration is idempotent and preserves an existing <see cref="ISecurityGrantStore"/> registration. The store is
        /// singleton and thread-safe, retains evidence only for the process lifetime, and is suitable for deterministic tests,
        /// examples, and short-lived hosts. A durable host selects a dedicated storage adapter instead.
        /// </remarks>
        public IServiceCollection AddInMemorySecurityGrantStore()
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<ISecurityGrantStore, InMemorySecurityGrantStore>();
            return services;
        }
    }
}
