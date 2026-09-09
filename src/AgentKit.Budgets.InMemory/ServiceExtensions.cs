// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory;

/// <summary>Provides explicit dependency-injection selection for the process-local budget ledger adapter.</summary>
/// <remarks>The budget runtime and application-selected dimension catalog remain separate registrations; this leaf supplies only storage and its replaceable process-local collaborators.</remarks>
public static class ServiceExtensions
{
    /// <summary>Adds budget-ledger registration operations to a caller-owned service collection.</summary>
    /// <param name="services">The mutable application composition receiving the explicit adapter selection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>Adds one singleton ephemeral ledger and replaceable clock and typed identity generators.</summary>
        /// <returns>The same caller-owned service collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>Repeating this exact leaf registration is idempotent. Existing clocks and typed identity generators are preserved. A distinct or custom <see cref="IBudgetLedger"/> registration remains visible so composition can reject ambiguity. The caller must separately register the budget runtime and one <see cref="IBudgetDimensionCatalog"/>.</remarks>
        public IServiceCollection AddInMemoryBudgetLedger()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<BudgetScopeId>, GuidBudgetScopeIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<BudgetReservationId>, GuidBudgetReservationIdGenerator>();
            if (!services.Any(static descriptor =>
                    descriptor.ServiceType == typeof(IBudgetLedger)
                    && descriptor.Lifetime == ServiceLifetime.Singleton
                    && descriptor.ImplementationType == typeof(InMemoryBudgetLedger)))
            {
                services.Add(ServiceDescriptor.Singleton<IBudgetLedger, InMemoryBudgetLedger>());
            }
            return services;
        }
    }
}
