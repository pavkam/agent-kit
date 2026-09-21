// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers first-party context services and validates their options.</summary>
internal static class AgentContextRegistration
{
    /// <summary>Registers shared context infrastructure used by every assembler registration.</summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Optional configuration applied on first registration.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
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

        services.TryAddSingleton<IContextMessageTokenEstimator, CharacterBasedContextMessageTokenEstimator>();
        services.TryAddSingleton<IContextBudgetAllocator, DefaultContextBudgetAllocator>();
        return services;
    }
}
