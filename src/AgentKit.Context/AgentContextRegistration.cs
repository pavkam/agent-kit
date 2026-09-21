// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers first-party context services and validates their options.</summary>
internal static class AgentContextRegistration
{
    /// <summary>Registers the built-in assembler under an explicit key.</summary>
    internal static IServiceCollection Add(
        IServiceCollection services,
        ComponentKey<IContextAssembler> key,
        Action<AgentContextOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        EnsureNoConflictingAssemblerRegistration<DefaultContextAssembler>(services, key.Value);

        _ = RegisterSharedInfrastructure(services, configure);

        services.TryAddKeyedScoped<IContextAssembler>(key.Value, static (provider, serviceKey) =>
        {
            var assemblerKey = (string) serviceKey!;
            var servicesBundle = ContextAssemblerServicesFactory.Create(provider, assemblerKey);
            return new DefaultContextAssembler(
                servicesBundle,
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILogger<DefaultContextAssembler>>());
        });

        if (key.Equals(AgentContextComponentDefaults.AssemblerKey))
        {
            services.TryAddSingleton(static provider =>
                provider.GetRequiredKeyedService<IContextAssembler>(AgentContextComponentDefaults.AssemblerKey.Value));
        }

        return services;
    }

    /// <summary>Additively registers a custom keyed assembler implementation.</summary>
    internal static IServiceCollection AddAssembler<TAssembler>(IServiceCollection services, ComponentKey<IContextAssembler> key)
        where TAssembler : class, IContextAssembler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        EnsureNoConflictingAssemblerRegistration<TAssembler>(services, key.Value);
        services.TryAddKeyedScoped<IContextAssembler, TAssembler>(key.Value);
        return services;
    }

    /// <summary>Replaces the assembler registered under a key.</summary>
    internal static IServiceCollection ReplaceAssembler<TAssembler>(IServiceCollection services, ComponentKey<IContextAssembler> key)
        where TAssembler : class, IContextAssembler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        RemoveKeyed<IContextAssembler>(services, key.Value);
        _ = services.AddKeyedScoped<IContextAssembler, TAssembler>(key.Value);
        return services;
    }

    /// <summary>Additively registers one contributor for an assembler profile.</summary>
    internal static IServiceCollection AddContributor<TContributor>(
        IServiceCollection services,
        ComponentKey<IContextAssembler> assemblerKey,
        ContextContributorRegistration registration)
        where TContributor : class, IContextContributor
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblerKey.Value, nameof(assemblerKey));
        ArgumentNullException.ThrowIfNull(registration);

        var existing = services
            .Select(static descriptor => descriptor.ImplementationInstance as ContextContributorDeclaration)
            .FirstOrDefault(declaration =>
                declaration is not null
                && assemblerKey.Value.Equals(declaration.AssemblerKey, StringComparison.Ordinal)
                && declaration.Registration.ContributorId.Equals(registration.ContributorId));
        if (existing is not null)
        {
            return existing.Registration.Equals(registration) && existing.ContributorType == typeof(TContributor)
                ? services
                : throw new InvalidOperationException(
                $"A different context contributor is already registered under contributor id '{registration.ContributorId.Value}' " +
                $"for assembler key '{assemblerKey.Value}'.");
        }

        _ = services.AddSingleton(new ContextContributorDeclaration(
            assemblerKey.Value,
            registration,
            typeof(TContributor)));
        services.TryAddScoped<TContributor>();
        return services;
    }

    /// <summary>Replaces the budget allocator used by one assembler profile.</summary>
    internal static IServiceCollection ReplaceBudgetAllocator<TAllocator>(
        IServiceCollection services,
        ComponentKey<IContextAssembler> assemblerKey)
        where TAllocator : class, IContextBudgetAllocator
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblerKey.Value, nameof(assemblerKey));
        RemoveKeyed<IContextBudgetAllocator>(services, assemblerKey.Value);
        _ = services.AddKeyedSingleton<IContextBudgetAllocator, TAllocator>(assemblerKey.Value);
        return services;
    }

    /// <summary>Registers shared context infrastructure used by every assembler registration.</summary>
    internal static IServiceCollection RegisterSharedInfrastructure(
        IServiceCollection services,
        Action<AgentContextOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        var optionsBuilder = services.AddOptions<AgentContextOptions>()
            .Validate(static o => o.ReservedOutputTokens >= 0, "ReservedOutputTokens must not be negative.")
            .Validate(static o => o.ProviderOverheadTokens >= 0, "ProviderOverheadTokens must not be negative.")
            .Validate(static o => o.EstimationSafetyMargin is >= 0 and <= 1, "EstimationSafetyMargin must be in [0, 1].")
            .Validate(static o => o.EstimatedCharactersPerToken > 0, "EstimatedCharactersPerToken must be positive.");
        if (configure is not null)
        {
            _ = optionsBuilder.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IContextMessageTokenEstimator, CharacterBasedContextMessageTokenEstimator>();
        services.TryAddSingleton<IContextBudgetAllocator, DefaultContextBudgetAllocator>();
        services.TryAddSingleton<IInstructionResolver, DefaultInstructionResolver>();
        services.TryAddSingleton<IHistoryPipeline, DefaultHistoryPipeline>();
        services.TryAddSingleton<IToolSnapshotProvider, StaticToolSnapshotProvider>();
        return services;
    }

    private static void EnsureNoConflictingAssemblerRegistration<TAssembler>(IServiceCollection services, string key)
        where TAssembler : class, IContextAssembler
    {
        var conflict = services.FirstOrDefault(descriptor =>
            descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(IContextAssembler)
            && key.Equals(descriptor.ServiceKey as string, StringComparison.Ordinal)
            && descriptor.KeyedImplementationType is not null
            && descriptor.KeyedImplementationType != typeof(TAssembler)
            && descriptor.ImplementationFactory is null);
        if (conflict is not null)
        {
            throw new InvalidOperationException(
                $"A different IContextAssembler implementation ('{conflict.KeyedImplementationType!.Name}') is already " +
                $"registered under key '{key}'. Use ReplaceContextAssembler to replace it explicitly.");
        }
    }

    private static void RemoveKeyed<TService>(IServiceCollection services, string key)
    {
        var descriptors = services
            .Where(descriptor => descriptor.IsKeyedService
                && descriptor.ServiceType == typeof(TService)
                && key.Equals(descriptor.ServiceKey as string, StringComparison.Ordinal))
            .ToArray();
        foreach (var descriptor in descriptors)
        {
            _ = services.Remove(descriptor);
        }
    }
}
