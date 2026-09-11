// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Owns exact typed-source registration mutations after immutable binding validation.</summary>
/// <remarks>Registration operates on service descriptors only and never activates provider or invoker instances.</remarks>
internal static class ToolServiceRegistration
{
    /// <summary>Registers or explicitly replaces one already validated static source without choosing unkeyed fallbacks.</summary>
    /// <param name="services">The nonnull mutable collection whose exact source key is changed.</param>
    /// <param name="bindings">The nonnull immutable borrowed binding graph to retain.</param>
    /// <param name="replace">Whether to remove all existing provider registrations for this exact typed key; false rejects any duplicate.</param>
    /// <returns>The same collection with logging, a preserved or default clock, and one keyed provider factory.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="bindings"/> is null.</exception>
    /// <exception cref="ArgumentException">Replacement is disabled and the typed source key is already registered.</exception>
    internal static IServiceCollection RegisterStaticProvider(IServiceCollection services, ToolProviderBindings bindings, bool replace)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(bindings);
        var sourceId = bindings.Snapshot.SourceId;
        var registrations = services.Where(descriptor => descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(IToolProvider)
            && descriptor.ServiceKey is ToolSourceId candidate && candidate == sourceId).ToArray();
        if (!replace)
        {
            ArgumentException.ThrowIfNotEqual(registrations.Length, 0, nameof(services));
        }
        foreach (var registration in registrations)
        {
            _ = services.Remove(registration);
        }
        _ = services.AddAgentKitObservability();
        services.TryAddSingleton(TimeProvider.System);
        _ = services.AddKeyedSingleton<IToolProvider>(sourceId, (provider, _) => new StaticToolProvider(
            bindings, provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<ILogger<StaticToolProvider>>(),
            provider.GetRequiredService<ILogger<ToolProviderCapture>>()));
        return services;
    }
}
