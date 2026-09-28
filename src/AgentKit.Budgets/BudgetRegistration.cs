// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>Package-internal dependency-injection registration for the budget runtime.</summary>
internal static class BudgetRegistration
{
    internal static IServiceCollection AddDefault(IServiceCollection services, Action<AgentBudgetOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddAgentKitObservability();
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

        services.TryAddSingleton<IBudgetDimensionCatalog, InMemoryBudgetDimensionCatalog>();
        services.TryAddSingleton<BudgetProfileRegistry>();
        services.TryAddSingleton(static provider => new BudgetProfileRegistryInitializer(
            provider.GetRequiredService<BudgetProfileRegistry>(),
            provider.GetServices<IBudgetProfileContributor>()));
        services.TryAddSingleton<IBudgetProfileCatalog>(static provider =>
        {
            _ = provider.GetRequiredService<BudgetProfileRegistryInitializer>();
            return new InMemoryBudgetProfileCatalog(provider.GetRequiredService<BudgetProfileRegistry>());
        });
        services.TryAddSingleton<IBudgetPolicyCatalog, InMemoryBudgetPolicyCatalog>();
        services.TryAddSingleton<IBudgetAuthority>(static provider =>
        {
            var ledgers = provider.GetServices<IBudgetLedger>().Take(2).ToArray();
            return ledgers.Length == 1
                ? new BudgetAuthority(
                    ledgers[0],
                    provider.GetRequiredService<IBudgetProfileCatalog>(),
                    provider.GetRequiredService<IBudgetPolicyCatalog>(),
                    provider.GetRequiredService<AgentBudgetOptionsSnapshot>(),
                    provider.GetService<ILoggerFactory>())
                : throw new InvalidOperationException(
                    "The first-party budget runtime requires exactly one explicitly selected unkeyed IBudgetLedger.");
        });

        return services;
    }

    internal static IServiceCollection AddProfile(IServiceCollection services, BudgetProfileKey key, Action<BudgetProfileOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(configure);
        _ = AddDefault(services, configure: null);
        _ = services.AddSingleton<IBudgetProfileContributor>(new BudgetProfileContributor(key, configure, replace: false));
        return services;
    }

    internal static IServiceCollection ReplaceProfile(IServiceCollection services, BudgetProfileKey key, Action<BudgetProfileOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(configure);
        _ = AddDefault(services, configure: null);
        RemoveProfileContributors(services, key);
        _ = services.AddSingleton<IBudgetProfileContributor>(new BudgetProfileContributor(key, configure, replace: true));
        return services;
    }

    internal static IServiceCollection ReplaceAuthority<TAuthority>(IServiceCollection services)
        where TAuthority : class, IBudgetAuthority
    {
        _ = services.RemoveAll<IBudgetAuthority>();
        _ = services.AddSingleton<IBudgetAuthority, TAuthority>();
        return services;
    }

    internal static IServiceCollection ReplaceLedger<TLedger>(IServiceCollection services)
        where TLedger : class, IBudgetLedger
    {
        _ = services.RemoveAll<IBudgetLedger>();
        _ = services.AddSingleton<IBudgetLedger, TLedger>();
        return services;
    }

    internal static IServiceCollection ReplaceProfileCatalog<TCatalog>(IServiceCollection services)
        where TCatalog : class, IBudgetProfileCatalog
    {
        _ = services.RemoveAll<IBudgetProfileCatalog>();
        _ = services.AddSingleton<IBudgetProfileCatalog, TCatalog>();
        return services;
    }

    internal static IServiceCollection ReplacePolicyCatalog<TCatalog>(IServiceCollection services)
        where TCatalog : class, IBudgetPolicyCatalog
    {
        _ = services.RemoveAll<IBudgetPolicyCatalog>();
        _ = services.AddSingleton<IBudgetPolicyCatalog, TCatalog>();
        return services;
    }

    internal static IServiceCollection ReplaceDimensionCatalog<TCatalog>(IServiceCollection services)
        where TCatalog : class, IBudgetDimensionCatalog
    {
        _ = services.RemoveAll<IBudgetDimensionCatalog>();
        _ = services.AddSingleton<IBudgetDimensionCatalog, TCatalog>();
        return services;
    }

    internal static IServiceCollection AddPolicy<TPolicy>(IServiceCollection services, BudgetPolicyKey key)
        where TPolicy : class, IBudgetPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        _ = AddDefault(services, configure: null);
        _ = services.AddKeyedSingleton<IBudgetPolicy, TPolicy>(key.Value);
        return services;
    }

    internal static IServiceCollection ReplacePolicy<TPolicy>(IServiceCollection services, BudgetPolicyKey key)
        where TPolicy : class, IBudgetPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        _ = services.RemoveAllKeyed<IBudgetPolicy>(key.Value);
        return AddPolicy<TPolicy>(services, key);
    }

    internal static IServiceCollection AddDimension(IServiceCollection services, BudgetDimensionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return services.AddSingleton(descriptor);
    }

    internal static IServiceCollection ReplaceDimension(IServiceCollection services, BudgetDimensionDescriptor descriptor)
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

    private static void RemoveProfileContributors(IServiceCollection services, BudgetProfileKey key)
    {
        var existing = services
            .Where(service =>
                service.ServiceType == typeof(IBudgetProfileContributor)
                && service.ImplementationInstance is BudgetProfileContributor contributor
                && contributor.Key.Equals(key))
            .ToList();
        foreach (var service in existing)
        {
            _ = services.Remove(service);
        }
    }

    private sealed class DefaultDimensionsSeeded;
}
