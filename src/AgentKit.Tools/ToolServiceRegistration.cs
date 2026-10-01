// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

        _ = services.AddKeyedSingleton<IToolProvider>(sourceId, static (provider, _) => new ApplicationToolProvider(
            provider,
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<ToolProviderCapture>>()));
        _ = services.AddSingleton(new ToolProviderRegistration(sourceId));
        return services.AddToolRegistrationCatalog();
    }

    /// <summary>Registers or explicitly replaces the execution policy for one exact reference without choosing an unkeyed fallback.</summary>
    /// <typeparam name="TPolicy">The policy whose <see cref="IToolExecutionPolicy.Reference"/> must equal <paramref name="reference"/>.</typeparam>
    /// <param name="services">The nonnull mutable collection; no service is activated.</param>
    /// <param name="reference">The nonnull exact reference, used as the DI service key.</param>
    /// <param name="replace">Whether to remove every existing registration and marker for this exact reference; false rejects a duplicate.</param>
    /// <returns>The same collection with one keyed policy, its marker, and the replaceable selector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="reference"/> is null.</exception>
    /// <exception cref="ArgumentException">Replacement is disabled and the exact reference is already registered, or internal markers cannot be inspected without activation.</exception>
    internal static IServiceCollection AddExecutionPolicy<TPolicy>(IServiceCollection services, ToolExecutionPolicyReference reference, bool replace)
        where TPolicy : class, IToolExecutionPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(reference);
        var registrations = services.Where(descriptor => descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(IToolExecutionPolicy)
            && descriptor.ServiceKey is ToolExecutionPolicyReference key && key == reference).ToArray();
        var markers = services.Where(static descriptor => !descriptor.IsKeyedService && descriptor.ServiceType == typeof(ToolExecutionPolicyRegistration)).ToArray();
        ArgumentException.ThrowIfNotEqual(markers.All(static marker => marker.ImplementationInstance is ToolExecutionPolicyRegistration), true, nameof(services));
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
            if (((ToolExecutionPolicyRegistration) marker.ImplementationInstance!).Reference == reference)
            {
                _ = services.Remove(marker);
            }
        }

        services.Add(ServiceDescriptor.KeyedSingleton<IToolExecutionPolicy, TPolicy>(reference));
        _ = services.AddSingleton(new ToolExecutionPolicyRegistration(reference));
        return AddExecutionPolicySelector(services);
    }

    /// <summary>Registers the replaceable selector over the materialized keyed execution policies.</summary>
    /// <param name="services">The nonnull mutable collection.</param>
    /// <returns>The same collection with the selector registered once.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    internal static IServiceCollection AddExecutionPolicySelector(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(CreateExecutionPolicySelector);
        return services;
    }

    /// <summary>Materializes the exact keyed execution policies once at host composition.</summary>
    /// <param name="provider">The nonnull host provider used only while constructing the immutable selector.</param>
    /// <returns>A selector holding no service-provider reference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A registered reference has no or several keyed policies.</exception>
    /// <exception cref="ArgumentException">A policy's own reference differs from the reference it is registered under.</exception>
    internal static IToolExecutionPolicySelector CreateExecutionPolicySelector(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var policies = new Dictionary<ToolExecutionPolicyReference, IToolExecutionPolicy>();
        foreach (var reference in provider.GetServices<ToolExecutionPolicyRegistration>().Select(static marker => marker.Reference).Distinct())
        {
            var registered = provider.GetKeyedServices<IToolExecutionPolicy>(reference).ToArray();
            if (registered.Length != 1)
            {
                throw new InvalidOperationException("Each registered tool execution policy reference requires exactly one keyed policy.");
            }

            policies[reference] = registered[0];
        }

        return new ToolExecutionPolicySelector(policies, provider.GetRequiredService<ILogger<ToolExecutionPolicySelector>>());
    }

    /// <summary>Registers the tool-event dispatcher and its dependencies once.</summary>
    /// <param name="services">The nonnull mutable collection.</param>
    /// <returns>The same collection with the dispatcher, options, clock, and diagnostics registered.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    internal static IServiceCollection AddEventDispatcher(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddAgentKitObservability();
        _ = services.AddOptions<ToolRuntimeOptions>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ToolRuntimeOptions>, ToolRuntimeOptionsValidator>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ToolEventDispatcher>();
        return services;
    }

    /// <summary>Registers one additive tool-event sink under a unique identity.</summary>
    /// <typeparam name="TSink">The concurrently callable singleton sink.</typeparam>
    /// <param name="services">The nonnull mutable collection; no service is activated.</param>
    /// <param name="registration">The nonnull declared identity and order.</param>
    /// <returns>The same collection with the sink, its declaration, and its binding registered.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="registration"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A different sink type or registration already uses the same identity.</exception>
    internal static IServiceCollection AddEventSink<TSink>(IServiceCollection services, ToolEventSinkRegistration registration)
        where TSink : class, IToolEventSink
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registration);
        _ = AddEventDispatcher(services);
        var existing = services.FirstOrDefault(descriptor =>
            descriptor.ServiceType == typeof(ToolEventSinkDeclaration)
            && descriptor.ImplementationInstance is ToolEventSinkDeclaration declaration
            && declaration.Registration.Id == registration.Id);
        if (existing?.ImplementationInstance is ToolEventSinkDeclaration previous)
        {
            return previous.SinkType == typeof(TSink) && previous.Registration == registration
                ? services
                : throw new InvalidOperationException($"A tool event sink with identity '{registration.Id.Value}' is already registered differently.");
        }

        services.TryAddSingleton<TSink>();
        _ = services.AddSingleton(new ToolEventSinkDeclaration(registration, typeof(TSink)));
        return services.AddSingleton(provider => new ToolEventSinkBinding(registration, provider.GetRequiredService<TSink>()));
    }

    /// <summary>Registers or explicitly replaces the recorder selected by one exact executor key.</summary>
    /// <typeparam name="TRecorder">The recorder bound to <paramref name="executor"/>.</typeparam>
    /// <param name="services">The nonnull mutable collection; no service is activated.</param>
    /// <param name="executor">The nondefault executor key that selects this recorder; an unkeyed recorder never satisfies it.</param>
    /// <param name="replace">Whether to remove every recorder already registered under the exact key; false rejects a duplicate.</param>
    /// <returns>The same collection with one keyed recorder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="executor"/> is default.</exception>
    /// <exception cref="ArgumentException">Replacement is disabled and the exact key already has a recorder.</exception>
    internal static IServiceCollection AddRecorder<TRecorder>(IServiceCollection services, ComponentKey<IToolExecutor> executor, bool replace)
        where TRecorder : class, IToolCallRecorder
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfEqual(executor, default);
        var existing = services.Where(descriptor => descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(IToolCallRecorder)
            && executor.Value.Equals(descriptor.ServiceKey as string, StringComparison.Ordinal)).ToArray();
        if (!replace)
        {
            ArgumentException.ThrowIfNotEqual(existing.Length, 0, nameof(services));
        }

        foreach (var descriptor in existing)
        {
            _ = services.Remove(descriptor);
        }

        services.Add(ServiceDescriptor.KeyedSingleton<IToolCallRecorder, TRecorder>(executor.Value));
        return services;
    }

    /// <summary>Explicitly replaces every unkeyed registration of one singular tool-runtime axis.</summary>
    /// <typeparam name="TService">The singular axis contract.</typeparam>
    /// <typeparam name="TImplementation">The replacement singleton implementation.</typeparam>
    /// <param name="services">The nonnull mutable collection; no service is activated.</param>
    /// <returns>The same collection with exactly one unkeyed registration of <typeparamref name="TService"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <remarks>Keyed registrations of the same contract and already-built hosts are unchanged. Later default registration preserves this choice.</remarks>
    internal static IServiceCollection ReplaceSingular<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (var descriptor in services.Where(static descriptor => !descriptor.IsKeyedService && descriptor.ServiceType == typeof(TService)).ToArray())
        {
            _ = services.Remove(descriptor);
        }

        return services.AddSingleton<TService, TImplementation>();
    }

    /// <summary>Builds the first-party executor over the host's singular collaborators and one selected recorder.</summary>
    /// <param name="provider">The nonnull host provider used while constructing the executor.</param>
    /// <param name="recorder">The nonnull recorder this executor commits accepted and terminal records through.</param>
    /// <param name="resultSpill">The result spill selected for this executor, or <see langword="null"/> when oversized results are truncated.</param>
    /// <returns>The immutable executor.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal static DefaultToolExecutor CreateExecutor(IServiceProvider provider, IToolCallRecorder recorder, IToolResultSpill? resultSpill)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(recorder);
        return new DefaultToolExecutor(
            provider.GetRequiredService<IToolResolver>(),
            provider.GetRequiredService<IToolArgumentValidator>(),
            provider.GetRequiredService<IToolExecutionPolicySelector>(),
            provider.GetRequiredService<ISecurityAuthoritySelector>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityRequestId>>(),
            recorder,
            provider.GetRequiredService<IToolScheduler>(),
            provider.GetRequiredService<ToolEventDispatcher>(),
            provider.GetRequiredService<ToolSchemaLimits>(),
            provider.GetRequiredService<IOptions<ToolRuntimeOptions>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<DefaultToolExecutor>>(),
            provider.GetService<IHookDispatcher>(),
            provider.GetService<IApprovalWaitRecorder>(),
            resultSpill);
    }
}
