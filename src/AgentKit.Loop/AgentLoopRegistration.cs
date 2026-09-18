// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Implements the keyed, scoped dependency-injection registration behind <see cref="ServiceExtensions"/>.</summary>
/// <remarks>
/// Kept separate from the public <c>extension(IServiceCollection)</c> block so the registration logic — option
/// validation, conflict diagnosis, and shared engine-wide infrastructure — can be unit tested and reused by
/// every overload without duplicating it inside the public surface.
/// </remarks>
internal static class AgentLoopRegistration
{
    /// <summary>Registers the built-in <see cref="DefaultAgentLoop"/> as a keyed, scoped <see cref="IAgentLoop"/>.</summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="key">The stable key this loop registration is selected by.</param>
    /// <param name="configure">Optional configuration applied only the first time this exact key is registered.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is uninitialized.</exception>
    /// <exception cref="InvalidOperationException">
    /// A different <see cref="IAgentLoop"/> implementation is already registered under <paramref name="key"/>.
    /// </exception>
    internal static IServiceCollection AddDefault(
        IServiceCollection services,
        ComponentKey<IAgentLoop> key,
        Action<AgentLoopOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        EnsureNoConflictingLoopRegistration<DefaultAgentLoop>(services, key);

        RegisterSharedInfrastructure(services);

        var optionsBuilder = services.AddOptions<AgentLoopOptions>(key.Value)
            .Validate(static o => o.HistoryReadPageSize > 0, "HistoryReadPageSize must be positive.")
            .Validate(static o => o.AppendConflictRetryLimit >= 0, "AppendConflictRetryLimit must not be negative.")
            .Validate(static o => o.SettlementTimeout > TimeSpan.Zero, "SettlementTimeout must be positive.")
            .Validate(static o => o.ObserverDeliveryTimeout > TimeSpan.Zero, "ObserverDeliveryTimeout must be positive.")
            .Validate(static o => o.ContextPressureThreshold is > 0 and <= 1, "ContextPressureThreshold must be in (0, 1].")
            .Validate(static o => o.EstimatedCharactersPerToken > 0, "EstimatedCharactersPerToken must be positive.");
        if (configure is not null)
        {
            _ = optionsBuilder.Configure(configure);
        }

        services.TryAddKeyedScoped<IAgentLoop, DefaultAgentLoop>(key.Value);
        services.TryAddKeyedSingleton<IRunContinuationPolicy, DefaultRunContinuationPolicy>(
            AgentLoopDefaults.ContinuationPolicyKey.Value);

        return services;
    }

    /// <summary>Additively registers a custom keyed, scoped <see cref="IAgentLoop"/> implementation.</summary>
    /// <typeparam name="TLoop">The scoped loop implementation.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="key">The stable key this loop registration is selected by.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is uninitialized.</exception>
    /// <exception cref="InvalidOperationException">
    /// A different <see cref="IAgentLoop"/> implementation is already registered under <paramref name="key"/>.
    /// </exception>
    internal static IServiceCollection Add<TLoop>(IServiceCollection services, ComponentKey<IAgentLoop> key)
        where TLoop : class, IAgentLoop
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        EnsureNoConflictingLoopRegistration<TLoop>(services, key);
        services.TryAddKeyedScoped<IAgentLoop, TLoop>(key.Value);
        return services;
    }

    /// <summary>Replaces whatever <see cref="IAgentLoop"/> is registered under a key with a new implementation.</summary>
    /// <typeparam name="TLoop">The scoped replacement implementation.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="key">The stable key whose registration is replaced.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is uninitialized.</exception>
    internal static IServiceCollection Replace<TLoop>(IServiceCollection services, ComponentKey<IAgentLoop> key)
        where TLoop : class, IAgentLoop
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        RemoveKeyed<IAgentLoop>(services, key.Value);
        _ = services.AddKeyedScoped<IAgentLoop, TLoop>(key.Value);
        return services;
    }

    /// <summary>Additively registers a singleton continuation policy under an explicit key.</summary>
    /// <typeparam name="TPolicy">The stateless, thread-safe policy implementation.</typeparam>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="key">The stable policy key selected by an agent definition.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is uninitialized.</exception>
    internal static IServiceCollection AddContinuationPolicy<TPolicy>(
        IServiceCollection services,
        ComponentKey<IRunContinuationPolicy> key)
        where TPolicy : class, IRunContinuationPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        _ = services.AddKeyedSingleton<IRunContinuationPolicy, TPolicy>(key.Value);
        return services;
    }

    /// <summary>Registers the engine-wide, key-independent infrastructure every keyed loop registration shares.</summary>
    /// <param name="services">The service collection to register into.</param>
    /// <remarks>
    /// Every registration uses <c>TryAdd</c> semantics, so calling this once per <see cref="AddDefault"/>
    /// invocation is safe: the identifier generators and clock are shared across every keyed loop in one engine
    /// rather than duplicated per key, because they are genuinely key-independent mechanics.
    /// </remarks>
    private static void RegisterSharedInfrastructure(IServiceCollection services)
    {
        _ = services.AddAgentKitObservability();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIdentifierGenerator<OperationId>>(
            _ => new GuidIdentifierGenerator<OperationId>(static value => new OperationId(value)));
        services.TryAddSingleton<IIdentifierGenerator<TurnId>>(
            _ => new GuidIdentifierGenerator<TurnId>(static value => new TurnId(value)));
        services.TryAddSingleton<IIdentifierGenerator<ModelRequestId>>(
            _ => new GuidIdentifierGenerator<ModelRequestId>(static value => new ModelRequestId(value)));
        services.TryAddSingleton<IIdentifierGenerator<MessageId>>(
            _ => new GuidIdentifierGenerator<MessageId>(static value => new MessageId(value)));
        services.TryAddSingleton<IIdentifierGenerator<SessionEntryId>>(
            _ => new GuidIdentifierGenerator<SessionEntryId>(static value => new SessionEntryId(value)));
    }

    /// <summary>Throws when a different <see cref="IAgentLoop"/> implementation is already registered under this exact key.</summary>
    /// <typeparam name="TLoop">The implementation this registration attempt is adding.</typeparam>
    /// <param name="services">The service collection to inspect.</param>
    /// <param name="key">The exact key being registered.</param>
    /// <remarks>
    /// Only descriptors whose <see cref="ServiceDescriptor.KeyedImplementationType"/> is known are inspected;
    /// a factory- or instance-based registration cannot be compared this way and is left to fail at resolution
    /// time instead, consistent with ordinary Microsoft DI keyed-registration behavior.
    /// </remarks>
    private static void EnsureNoConflictingLoopRegistration<TLoop>(IServiceCollection services, ComponentKey<IAgentLoop> key)
        where TLoop : class, IAgentLoop
    {
        var conflict = services.FirstOrDefault(descriptor =>
            descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(IAgentLoop)
            && key.Value.Equals(descriptor.ServiceKey as string, StringComparison.Ordinal)
            && descriptor.KeyedImplementationType is not null
            && descriptor.KeyedImplementationType != typeof(TLoop));
        if (conflict is not null)
        {
            throw new InvalidOperationException(
                $"A different IAgentLoop implementation ('{conflict.KeyedImplementationType!.Name}') is already " +
                $"registered under key '{key.Value}'. Use ReplaceAgentLoop to replace it explicitly.");
        }
    }

    /// <summary>Removes every existing keyed registration of <typeparamref name="TService"/> for one exact key.</summary>
    /// <typeparam name="TService">The keyed service contract.</typeparam>
    /// <param name="services">The service collection to mutate.</param>
    /// <param name="key">The exact key whose registrations are removed.</param>
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
