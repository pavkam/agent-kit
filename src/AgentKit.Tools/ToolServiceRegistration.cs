// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns explicit toolset/source registration and composition-time binding capture.</summary>
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
        var descriptor = ServiceDescriptor.KeyedSingleton<IToolProvider>(sourceId, (provider, _) => new StaticToolProvider(
            bindings, provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<ILogger<StaticToolProvider>>(),
            provider.GetRequiredService<ILogger<ToolProviderCapture>>()));
        return RegisterProvider(services, sourceId, descriptor, replace);
    }

    /// <summary>Registers a typed singleton provider and its composition marker after complete descriptor validation.</summary>
    /// <param name="services">The nonnull mutable collection to update without activating any service.</param>
    /// <param name="sourceId">The nondefault exact source key.</param>
    /// <param name="descriptor">The nonnull keyed singleton provider descriptor with that exact typed key.</param>
    /// <param name="replace">Whether to remove all existing provider descriptors and markers for this source.</param>
    /// <returns>The same collection with one exact provider registration and the replaceable registration catalog.</returns>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceId"/> is default.</exception>
    /// <exception cref="ArgumentException">The descriptor is malformed, metadata cannot be inspected without activation, or addition would duplicate a typed source.</exception>
    internal static IServiceCollection RegisterProvider(IServiceCollection services, ToolSourceId sourceId, ServiceDescriptor descriptor, bool replace)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfEqual(sourceId, default);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNotEqual(descriptor.ServiceType, typeof(IToolProvider), nameof(descriptor));
        ArgumentException.ThrowIfNotEqual(descriptor.IsKeyedService && descriptor.ServiceKey is ToolSourceId key && key == sourceId
            && descriptor.Lifetime == ServiceLifetime.Singleton, true, nameof(descriptor));
        var registrations = services.Where(descriptor => descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(IToolProvider)
            && descriptor.ServiceKey is ToolSourceId candidate && candidate == sourceId).ToArray();
        var markers = services.Where(static registration => !registration.IsKeyedService && registration.ServiceType == typeof(ToolProviderRegistration)).ToArray();
        ArgumentException.ThrowIfNotEqual(markers.All(static marker => marker.ImplementationInstance is ToolProviderRegistration), true, nameof(services));
        if (!replace)
        {
            ArgumentException.ThrowIfNotEqual(registrations.Length, 0, nameof(services));
        }
        foreach (var registration in registrations)
        {
            _ = services.Remove(registration);
        }
        foreach (var marker in markers)
        {
            if (((ToolProviderRegistration) marker.ImplementationInstance!).SourceId == sourceId) { _ = services.Remove(marker); }
        }
        services.Add(descriptor);
        _ = services.AddSingleton(new ToolProviderRegistration(sourceId));
        return services.AddToolRegistrationCatalog();
    }

    /// <summary>Registers or replaces one complete immutable toolset publication under its exact typed key.</summary>
    /// <param name="services">The nonnull mutable service collection.</param>
    /// <param name="publication">The nonnull fully validated authored publication.</param>
    /// <param name="replace">Whether to replace all registrations and markers for the exact key.</param>
    /// <returns>The same collection with one keyed publication and the replaceable registration catalog.</returns>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentException">Addition would duplicate the exact key, or metadata cannot be inspected without activation.</exception>
    /// <remarks>Source references are validated when the materialized catalog is constructed, allowing registration in either order. No provider is activated here.</remarks>
    internal static IServiceCollection RegisterToolset(IServiceCollection services, ToolsetPublication publication, bool replace)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(publication);
        var registrations = services.Where(descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(ToolsetPublication)
            && descriptor.ServiceKey is ToolsetKey key && key == publication.Key).ToArray();
        var markers = services.Where(static descriptor => !descriptor.IsKeyedService && descriptor.ServiceType == typeof(ToolsetRegistration)).ToArray();
        ArgumentException.ThrowIfNotEqual(markers.All(static marker => marker.ImplementationInstance is ToolsetRegistration), true, nameof(services));
        if (!replace) { ArgumentException.ThrowIfNotEqual(registrations.Length, 0, nameof(services)); }
        foreach (var registration in registrations) { _ = services.Remove(registration); }
        foreach (var marker in markers)
        {
            if (((ToolsetRegistration) marker.ImplementationInstance!).Key == publication.Key) { _ = services.Remove(marker); }
        }
        _ = services.AddKeyedSingleton(publication.Key, publication);
        _ = services.AddSingleton(new ToolsetRegistration(publication.Key));
        return services.AddToolRegistrationCatalog();
    }

    /// <summary>Materializes explicit registration keys into borrowed bindings once at host composition.</summary>
    /// <param name="provider">The nonnull host provider used only while constructing the immutable catalog.</param>
    /// <returns>A complete registration view containing no service-provider reference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    /// <exception cref="InvalidOperationException">An explicit key has missing or multiple registrations, or a publication is null or declares a different key.</exception>
    /// <exception cref="ArgumentException">Provider source identity differs from its key or a toolset references an unregistered source.</exception>
    /// <remarks>Unkeyed and foreign-key registrations never become fallback sources or toolsets. Activated providers remain host-owned.</remarks>
    internal static IToolRegistrationCatalog CreateRegistrationCatalog(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var toolsets = ImmutableArray.CreateBuilder<ToolsetPublication>();
        foreach (var key in provider.GetServices<ToolsetRegistration>().Select(static marker => marker.Key).Distinct().OrderBy(static key => key.Value, StringComparer.Ordinal))
        {
            var publications = provider.GetKeyedServices<ToolsetPublication>(key).ToArray();
            if (publications.Length != 1 || publications[0] is not { } publication || publication.Key != key)
            {
                throw new InvalidOperationException("Each published toolset key requires exactly one matching publication.");
            }
            toolsets.Add(publication);
        }
        var bindings = ImmutableArray.CreateBuilder<ToolProviderBinding>();
        foreach (var key in provider.GetServices<ToolProviderRegistration>().Select(static marker => marker.SourceId).Distinct().OrderBy(static key => key.Value, StringComparer.Ordinal))
        {
            var sources = provider.GetKeyedServices<IToolProvider>(key).ToArray();
            if (sources.Length != 1)
            {
                throw new InvalidOperationException("Each registered tool source requires exactly one keyed provider.");
            }
            bindings.Add(new ToolProviderBinding(key, sources[0]));
        }
        return new ToolRegistrationCatalog(toolsets.ToImmutable(), bindings.ToImmutable(), provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<ILogger<ToolRegistrationCatalog>>());
    }

    /// <summary>Ensures the application-local tool provider is registered exactly once under its stable source id.</summary>
    /// <param name="services">The nonnull mutable collection.</param>
    /// <returns>The same collection with the application provider registered when absent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    internal static IServiceCollection EnsureApplicationToolProvider(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var sourceId = ApplicationToolSources.Default;
        var existing = services.Any(descriptor =>
            descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(IToolProvider)
            && descriptor.ServiceKey is ToolSourceId key
            && key == sourceId);
        if (existing)
        {
            return services.AddToolRegistrationCatalog();
        }

        _ = services.AddKeyedSingleton<IToolProvider, ApplicationToolProvider>(sourceId);
        _ = services.AddSingleton(new ToolProviderRegistration(sourceId));
        return services.AddToolRegistrationCatalog();
    }
}
