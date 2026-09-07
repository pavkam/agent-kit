// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Provides side-effect-free option capture and registration-time keyed descriptor mutation.</summary>
internal static class OutputRegistration
{
    /// <summary>Validates and captures one mutable option object.</summary>
    /// <param name="options">The configured options to capture.</param>
    /// <returns>An immutable validated snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A byte, node, or issue maximum is not positive; a depth is outside 1 through 128;
    /// or the repair-attempt maximum is negative.
    /// </exception>
    public static AgentOutputOptionsSnapshot CreateSnapshot(AgentOutputOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new AgentOutputOptionsSnapshot(
            options.MaximumCandidateBytes,
            options.MaximumSchemaBytes,
            options.MaximumSchemaDepth,
            options.MaximumSchemaNodes,
            options.MaximumCandidateDepth,
            options.MaximumCandidateNodes,
            options.MaximumValidationIssues,
            options.MaximumRepairAttempts,
            options.RequireSchemaForStructuredModes,
            options.AllowProviderModeDowngrade);
    }

    /// <summary>Removes every registration of one contract under one exact service key.</summary>
    /// <typeparam name="TService">The keyed service contract to remove.</typeparam>
    /// <param name="services">The mutable registration collection.</param>
    /// <param name="serviceKey">The validated exact key.</param>
    public static void RemoveKeyed<TService>(IServiceCollection services, object serviceKey)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceKey);
        for (var index = services.Count - 1; index >= 0; index--)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType == typeof(TService) && Equals(descriptor.ServiceKey, serviceKey))
            {
                services.RemoveAt(index);
            }
        }
    }
}
