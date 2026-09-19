// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Implements the keyed, additive dependency-injection registration behind <see cref="ServiceExtensions"/>'s IO surface.</summary>
/// <remarks>
/// Kept separate from the public <c>extension(IServiceCollection)</c> block so option validation, conflict
/// diagnosis, and shared engine-wide infrastructure can be unit tested and reused by every overload without
/// duplicating it inside the public surface, mirroring <c>AgentKit.Loop</c>'s <c>AgentLoopRegistration</c>.
/// </remarks>
internal static class AgentIORegistration
{
    /// <summary>Registers the built-in <see cref="DefaultInputCoordinator"/> and <see cref="DefaultOutputPublisher"/> under explicit keys.</summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="inputKey">The stable key this input coordinator registration is selected by.</param>
    /// <param name="outputKey">The stable key this output publisher registration is selected by.</param>
    /// <param name="configure">Optional configuration applied only the first time <see cref="AgentIOOptions"/> is bound.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="inputKey"/> or <paramref name="outputKey"/> is uninitialized.</exception>
    /// <exception cref="InvalidOperationException">
    /// A different <see cref="IInputCoordinator"/> or <see cref="IOutputPublisher"/> implementation is already registered under the matching key.
    /// </exception>
    internal static IServiceCollection Add(
        IServiceCollection services,
        ComponentKey<IInputCoordinator> inputKey,
        ComponentKey<IOutputPublisher> outputKey,
        Action<AgentIOOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputKey.Value, nameof(inputKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(outputKey.Value, nameof(outputKey));
        EnsureNoConflictingKeyedRegistration<IInputCoordinator, DefaultInputCoordinator>(services, inputKey.Value);
        EnsureNoConflictingKeyedRegistration<IOutputPublisher, DefaultOutputPublisher>(services, outputKey.Value);

        BindOptions(services, configure);
        RegisterSharedInfrastructure(services);

        // DefaultInputCoordinator additionally requires IOptions<InputCoordinatorOptions>. This binds it with
        // this package's own documented defaults so AddAgentIO is self-sufficient; a host that also calls
        // AddInputCoordinator(configure) or Configure<InputCoordinatorOptions> still composes normally, since
        // options binding is additive.
        _ = services.AddOptions<InputCoordinatorOptions>()
            .Validate(static value => value.MaximumInputParts >= 1, "MaximumInputParts must be at least 1.")
            .Validate(static value => value.PreprocessingConfigurationVersion != default, "PreprocessingConfigurationVersion must be set.");

        services.TryAddSingleton<IIdentifierGenerator<AdmissionId>, GuidAdmissionIdGenerator>();
        services.TryAddKeyedScoped<IInputCoordinator, DefaultInputCoordinator>(inputKey.Value);
        services.TryAddKeyedScoped<IOutputPublisher, DefaultOutputPublisher>(outputKey.Value);

        return services;
    }

    /// <summary>Additively registers a custom keyed, scoped <see cref="IInputCoordinator"/> implementation.</summary>
    internal static IServiceCollection AddInput<TCoordinator>(IServiceCollection services, ComponentKey<IInputCoordinator> key)
        where TCoordinator : class, IInputCoordinator
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        EnsureNoConflictingKeyedRegistration<IInputCoordinator, TCoordinator>(services, key.Value);
        services.TryAddKeyedScoped<IInputCoordinator, TCoordinator>(key.Value);
        return services;
    }

    /// <summary>Replaces whatever <see cref="IInputCoordinator"/> is registered under a key with a new implementation.</summary>
    internal static IServiceCollection ReplaceInput<TCoordinator>(IServiceCollection services, ComponentKey<IInputCoordinator> key)
        where TCoordinator : class, IInputCoordinator
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        RemoveKeyed<IInputCoordinator>(services, key.Value);
        _ = services.AddKeyedScoped<IInputCoordinator, TCoordinator>(key.Value);
        return services;
    }

    /// <summary>Additively registers a custom keyed, scoped <see cref="IOutputPublisher"/> implementation.</summary>
    internal static IServiceCollection AddOutput<TPublisher>(IServiceCollection services, ComponentKey<IOutputPublisher> key)
        where TPublisher : class, IOutputPublisher
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        EnsureNoConflictingKeyedRegistration<IOutputPublisher, TPublisher>(services, key.Value);
        services.TryAddKeyedScoped<IOutputPublisher, TPublisher>(key.Value);
        return services;
    }

    /// <summary>Replaces whatever <see cref="IOutputPublisher"/> is registered under a key with a new implementation.</summary>
    internal static IServiceCollection ReplaceOutput<TPublisher>(IServiceCollection services, ComponentKey<IOutputPublisher> key)
        where TPublisher : class, IOutputPublisher
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        RemoveKeyed<IOutputPublisher>(services, key.Value);
        _ = services.AddKeyedScoped<IOutputPublisher, TPublisher>(key.Value);
        return services;
    }

    /// <summary>Additively registers one <see cref="IRunEventSink"/> under its declared stable name.</summary>
    /// <typeparam name="TSink">The sink implementation, resolved from the service provider when registered there, or constructed otherwise.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="registration">The sink's stable identity, delivery requirement, and fan-out order.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="registration"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A different sink registration or implementation type already uses <see cref="RunEventSinkRegistration.SinkName"/>.</exception>
    internal static IServiceCollection AddRunEventSink<TSink>(IServiceCollection services, RunEventSinkRegistration registration)
        where TSink : class, IRunEventSink
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registration);

        var existing = services
            .Select(static descriptor => descriptor.ImplementationInstance as RunEventSinkDeclaration)
            .FirstOrDefault(declaration => declaration is not null && declaration.Registration.SinkName == registration.SinkName);
        if (existing is not null)
        {
            return existing.Registration.Equals(registration) && existing.SinkType == typeof(TSink)
                ? services
                : throw new InvalidOperationException(
                    $"A different run-event sink registration already uses the name '{registration.SinkName}'.");
        }

        _ = services.AddSingleton(new RunEventSinkDeclaration(registration, typeof(TSink)));
        _ = services.AddSingleton<IRunEventSink>(provider =>
            new RunEventSinkBinding(registration, ActivatorUtilities.GetServiceOrCreateInstance<TSink>(provider)));
        return services;
    }

    /// <summary>Binds and validates <see cref="AgentIOOptions"/>, applying <paramref name="configure"/> when supplied.</summary>
    private static void BindOptions(IServiceCollection services, Action<AgentIOOptions>? configure)
    {
        var optionsBuilder = services.AddOptions<AgentIOOptions>()
            .Validate(static o => o.MaximumPendingInputsPerLane > 0, "MaximumPendingInputsPerLane must be positive.")
            .Validate(static o => o.MaximumFollowUpPromotionsPerIdleBoundary > 0, "MaximumFollowUpPromotionsPerIdleBoundary must be positive.")
            .Validate(static o => o.MaximumSubscriptions > 0, "MaximumSubscriptions must be positive.")
            .Validate(static o => o.CapacityPerSubscription > 0, "CapacityPerSubscription must be positive.")
            .Validate(static o => o.MaximumBestEffortSinkWait > TimeSpan.Zero, "MaximumBestEffortSinkWait must be positive.");
        if (configure is not null)
        {
            _ = optionsBuilder.Configure(configure);
        }
    }

    /// <summary>Registers the engine-wide, key-independent infrastructure every IO registration shares.</summary>
    private static void RegisterSharedInfrastructure(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IOutputBackpressurePolicy>(static provider =>
            new DefaultOutputBackpressurePolicy(provider.GetRequiredService<IOptions<AgentIOOptions>>().Value.MaximumBestEffortSinkWait));
        services.TryAddScoped(static provider =>
        {
            var identity = provider.GetRequiredService<RunScopeIdentity>();
            var options = provider.GetRequiredService<IOptions<AgentIOOptions>>().Value;
            return new RunEventHub(
                identity.AgentId,
                identity.SessionId,
                identity.ConversationId,
                identity.RunId,
                new RunEventHubOptions(options.MaximumSubscriptions, options.CapacityPerSubscription),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<RunEventHub>>());
        });
    }

    /// <summary>Tracks one sink's declared identity and implementation type for duplicate-name conflict detection.</summary>
    /// <remarks>This carries no service behavior; <see cref="RunEventSinkBinding"/> is the runtime <see cref="IRunEventSink"/> the same call also registers.</remarks>
    private sealed record RunEventSinkDeclaration(RunEventSinkRegistration Registration, Type SinkType);

    /// <summary>Throws when a different implementation is already registered under this exact key.</summary>
    private static void EnsureNoConflictingKeyedRegistration<TService, TImplementation>(IServiceCollection services, string key)
        where TImplementation : class, TService
    {
        var conflict = services.FirstOrDefault(descriptor =>
            descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(TService)
            && key.Equals(descriptor.ServiceKey as string, StringComparison.Ordinal)
            && descriptor.KeyedImplementationType is not null
            && descriptor.KeyedImplementationType != typeof(TImplementation));
        if (conflict is not null)
        {
            throw new InvalidOperationException(
                $"A different {typeof(TService).Name} implementation ('{conflict.KeyedImplementationType!.Name}') is already " +
                $"registered under key '{key}'. Use the explicit Replace method to replace it.");
        }
    }

    /// <summary>Removes every existing keyed registration of <typeparamref name="TService"/> for one exact key.</summary>
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
