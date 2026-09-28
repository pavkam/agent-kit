// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration for the first-party ledger-backed budget runtime.
/// </summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the ledger-backed budget authority and the first-party <c>agentkit.*</c> dimension descriptors.
        /// </summary>
        /// <param name="configure">Optional configuration for <see cref="AgentBudgetOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Idempotent: calling this more than once keeps the first runtime registration.
        /// This method never selects a storage adapter; applications must register exactly one
        /// unkeyed <see cref="IBudgetLedger"/> explicitly when this authority is selected.
        /// </remarks>
        public IServiceCollection AddAgentBudgets(Action<AgentBudgetOptions>? configure = null) =>
            BudgetRegistration.AddDefault(services, configure);

        /// <summary>Registers one immutable named budget profile.</summary>
        /// <param name="key">The profile key.</param>
        /// <param name="configure">The profile configuration callback.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddBudgetProfile(BudgetProfileKey key, Action<BudgetProfileOptions> configure) =>
            BudgetRegistration.AddProfile(services, key, configure);

        /// <summary>Replaces one named budget profile registration.</summary>
        /// <param name="key">The profile key.</param>
        /// <param name="configure">The replacement profile configuration callback.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceBudgetProfile(BudgetProfileKey key, Action<BudgetProfileOptions> configure) =>
            BudgetRegistration.ReplaceProfile(services, key, configure);

        /// <summary>Additively registers a custom <see cref="BudgetDimensionDescriptor"/>.</summary>
        /// <param name="descriptor">The descriptor to register.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddBudgetDimension(BudgetDimensionDescriptor descriptor) =>
            BudgetRegistration.AddDimension(services, descriptor);

        /// <summary>Replaces every previously registered descriptor for <paramref name="descriptor"/>'s dimension.</summary>
        /// <param name="descriptor">The replacement descriptor.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceBudgetDimension(BudgetDimensionDescriptor descriptor) =>
            BudgetRegistration.ReplaceDimension(services, descriptor);

        /// <summary>Replaces the singular <see cref="IBudgetAuthority"/> with <typeparamref name="TAuthority"/>.</summary>
        /// <typeparam name="TAuthority">The authority implementation to register.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceBudgetAuthority<TAuthority>()
            where TAuthority : class, IBudgetAuthority =>
            BudgetRegistration.ReplaceAuthority<TAuthority>(services);

        /// <summary>Replaces the singular unkeyed <see cref="IBudgetLedger"/> registration.</summary>
        /// <typeparam name="TLedger">The ledger implementation to register.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceBudgetLedger<TLedger>()
            where TLedger : class, IBudgetLedger =>
            BudgetRegistration.ReplaceLedger<TLedger>(services);

        /// <summary>Replaces the singular <see cref="IBudgetProfileCatalog"/>.</summary>
        /// <typeparam name="TCatalog">The catalog implementation to register.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceBudgetProfileCatalog<TCatalog>()
            where TCatalog : class, IBudgetProfileCatalog =>
            BudgetRegistration.ReplaceProfileCatalog<TCatalog>(services);

        /// <summary>Replaces the singular <see cref="IBudgetPolicyCatalog"/>.</summary>
        /// <typeparam name="TCatalog">The catalog implementation to register.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceBudgetPolicyCatalog<TCatalog>()
            where TCatalog : class, IBudgetPolicyCatalog =>
            BudgetRegistration.ReplacePolicyCatalog<TCatalog>(services);

        /// <summary>Replaces the singular <see cref="IBudgetDimensionCatalog"/>.</summary>
        /// <typeparam name="TCatalog">The catalog implementation to register.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceBudgetDimensionCatalog<TCatalog>()
            where TCatalog : class, IBudgetDimensionCatalog =>
            BudgetRegistration.ReplaceDimensionCatalog<TCatalog>(services);

        /// <summary>Registers one keyed <see cref="IBudgetPolicy"/> implementation.</summary>
        /// <typeparam name="TPolicy">The policy implementation.</typeparam>
        /// <param name="key">The policy key.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddBudgetPolicy<TPolicy>(BudgetPolicyKey key)
            where TPolicy : class, IBudgetPolicy =>
            BudgetRegistration.AddPolicy<TPolicy>(services, key);

        /// <summary>Replaces one keyed <see cref="IBudgetPolicy"/> registration.</summary>
        /// <typeparam name="TPolicy">The replacement policy implementation.</typeparam>
        /// <param name="key">The policy key.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceBudgetPolicy<TPolicy>(BudgetPolicyKey key)
            where TPolicy : class, IBudgetPolicy =>
            BudgetRegistration.ReplacePolicy<TPolicy>(services, key);
    }
}
