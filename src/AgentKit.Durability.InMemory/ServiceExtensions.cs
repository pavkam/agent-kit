// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Provides explicit dependency-injection selection for the process-local execution-lease manager.</summary>
/// <remarks>The durability runtime and its selected journal remain separate registrations; this leaf supplies only process-local lease coordination.</remarks>
public static class ServiceExtensions
{
    /// <summary>Adds execution-lease registration operations to a caller-owned service collection.</summary>
    /// <param name="services">The mutable application composition receiving the explicit adapter selection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>Adds one singleton process-local lease manager and replaceable clock and identity generator.</summary>
        /// <returns>The same caller-owned service collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>Repeating this exact leaf registration is idempotent. An existing clock or lease-identity generator is preserved. A distinct or custom <see cref="IDurableLeaseManager"/> registration remains visible so composition can reject ambiguity.</remarks>
        public IServiceCollection AddInMemoryDurableLeaseManager()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<ExecutionLeaseId>, GuidExecutionLeaseIdGenerator>();
            if (!services.Any(static descriptor =>
                    descriptor.ServiceType == typeof(IDurableLeaseManager)
                    && descriptor.Lifetime == ServiceLifetime.Singleton
                    && descriptor.ImplementationType == typeof(InMemoryDurableLeaseManager)))
            {
                services.Add(ServiceDescriptor.Singleton<IDurableLeaseManager, InMemoryDurableLeaseManager>());
            }
            return services;
        }
    }
}
