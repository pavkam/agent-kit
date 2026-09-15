// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory;

/// <summary>Registers deterministic process-local security-grant storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Adds explicitly ephemeral process-local approval storage.</summary>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddInMemoryApprovalStore()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton<IApprovalStore, InMemoryApprovalStore>();
            return services;
        }

        /// <summary>Adds the process-local store as one explicit security-grant adapter selection.</summary>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// Repeating this leaf is idempotent. A different leaf or custom store remains visible as another
        /// <see cref="ISecurityGrantStore"/> registration so composition rejects ambiguity regardless of registration order.
        /// Hosts replace a prior selection explicitly by removing its interface registrations before adding this leaf.
        /// </remarks>
        public IServiceCollection AddInMemorySecurityGrantStore()
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            if (!services.Any(static descriptor =>
                    descriptor.ServiceType == typeof(ISecurityGrantStore)
                    && descriptor.Lifetime == ServiceLifetime.Singleton
                    && descriptor.ImplementationType == typeof(InMemorySecurityGrantStore)))
            {
                services.Add(ServiceDescriptor.Singleton<ISecurityGrantStore, InMemorySecurityGrantStore>());
            }
            return services;
        }
    }
}
