// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency-injection registration for the built-in, in-memory
/// hierarchical budget authority.
/// </summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the built-in <see cref="InMemoryBudgetAuthority"/> and
        /// the first-party <c>agentkit.*</c> dimension descriptors.
        /// </summary>
        /// <param name="configure">Optional configuration for <see cref="AgentBudgetOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Idempotent: every registration here uses <c>TryAdd</c> semantics,
        /// so calling this more than once keeps the first registration.
        /// </remarks>
        public IServiceCollection AddAgentBudgets(Action<AgentBudgetOptions>? configure = null)
        {
            var optionsBuilder = services.AddOptions<AgentBudgetOptions>()
                .Validate(static o => o.MaximumScopeDepth > 0, "MaximumScopeDepth must be positive.")
                .Validate(
                    static o => o.MaximumOpenReservationsPerScope > 0,
                    "MaximumOpenReservationsPerScope must be positive.")
                .Validate(
                    static o => o.DefaultReservationLifetime > TimeSpan.Zero,
                    "DefaultReservationLifetime must be positive.");

            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

            services.TryAddSingleton(static provider =>
            {
                var options = provider.GetRequiredService<IOptions<AgentBudgetOptions>>().Value;
                return new AgentBudgetOptionsSnapshot(
                    options.MaximumScopeDepth,
                    options.MaximumOpenReservationsPerScope,
                    options.DefaultReservationLifetime,
                    options.UnknownCostBehavior,
                    options.OverrunBehavior);
            });

            var defaultsAlreadySeeded = services.Any(static service => service.ServiceType == typeof(DefaultDimensionsSeeded));
            services.TryAddSingleton<DefaultDimensionsSeeded>();

            if (!defaultsAlreadySeeded)
            {
                foreach (var descriptor in BudgetDimensionCatalogDefaults.Create())
                {
                    _ = services.AddSingleton(descriptor);
                }
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IBudgetDimensionCatalog, InMemoryBudgetDimensionCatalog>();
            services.TryAddSingleton<IIdentifierGenerator<BudgetScopeId>>(
                static _ => new GuidIdentifierGenerator<BudgetScopeId>(static value => new BudgetScopeId(value)));
            services.TryAddSingleton<IIdentifierGenerator<BudgetReservationId>>(
                static _ => new GuidIdentifierGenerator<BudgetReservationId>(static value => new BudgetReservationId(value)));
            services.TryAddSingleton<IBudgetAuthority, InMemoryBudgetAuthority>();

            return services;
        }

        /// <summary>Additively registers a custom <see cref="BudgetDimensionDescriptor"/>.</summary>
        /// <param name="descriptor">The descriptor to register.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is null.</exception>
        /// <remarks>
        /// Registering more than one descriptor for the same dimension fails
        /// when <see cref="IBudgetDimensionCatalog"/> is built. Use
        /// <see cref="ReplaceBudgetDimension"/> to override a first-party or
        /// previously registered dimension intentionally.
        /// </remarks>
        public IServiceCollection AddBudgetDimension(BudgetDimensionDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return services.AddSingleton(descriptor);
        }

        /// <summary>Replaces every previously registered descriptor for <paramref name="descriptor"/>'s dimension.</summary>
        /// <param name="descriptor">The replacement descriptor.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is null.</exception>
        public IServiceCollection ReplaceBudgetDimension(BudgetDimensionDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            var existing = services
                .Where(service =>
                    service.ServiceType == typeof(BudgetDimensionDescriptor)
                    && !service.IsKeyedService
                    && service.ImplementationInstance is BudgetDimensionDescriptor existingDescriptor
                    && existingDescriptor.Dimension.Equals(descriptor.Dimension))
                .ToList();

            foreach (var service in existing)
            {
                _ = services.Remove(service);
            }

            return services.AddSingleton(descriptor);
        }

        /// <summary>Replaces the singular <see cref="IBudgetAuthority"/> with <typeparamref name="TAuthority"/>.</summary>
        /// <typeparam name="TAuthority">The authority implementation to register.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceBudgetAuthority<TAuthority>()
            where TAuthority : class, IBudgetAuthority
        {
            _ = services.RemoveAll<IBudgetAuthority>();
            _ = services.AddSingleton<IBudgetAuthority, TAuthority>();
            return services;
        }
    }

    /// <summary>
    /// A marker type registered once <see cref="AddAgentBudgets"/> has already seeded the
    /// first-party <c>agentkit.*</c> dimension descriptors, so a repeated call does not register
    /// them a second time.
    /// </summary>
    private sealed class DefaultDimensionsSeeded;
}
