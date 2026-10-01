// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Implements the evaluation registrations behind <see cref="ServiceExtensions"/>.</summary>
/// <remarks>
/// Keyed registrations are additive and idempotent for the same key and implementation type; reusing a key for a different
/// implementation fails at registration instead of letting the last registration win. Replacement is a separate, deliberate call.
/// </remarks>
internal static class EvaluationRegistration
{
    /// <summary>Registers the singular runner, its options, the catalogs, the selector, and the identity and time defaults.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration; repeated calls compose their callbacks in call order.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="InvalidOperationException">More than one runner is already registered.</exception>
    internal static IServiceCollection AddDefault(IServiceCollection services, Action<EvaluationOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        AddDefaults(services);
        var builder = services.AddOptions<EvaluationOptions>()
            .Validate(static options => options.MaximumConcurrentCases > 0, "MaximumConcurrentCases must be positive.")
            .Validate(static options => options.MaximumRepetitions > 0, "MaximumRepetitions must be positive.")
            .Validate(static options => options.DefaultCaseTimeout > TimeSpan.Zero, "DefaultCaseTimeout must be positive.")
            .ValidateOnStart();
        if (configure is not null)
        {
            _ = builder.Configure(configure);
        }

        services.TryAddSingleton<IEvaluationRunner>(static provider =>
        {
            var engines = provider.GetServices<AgentEngine>().ToArray();
            return engines.Length == 1
                ? new EvaluationRunner(
                    engines[0],
                    provider.GetRequiredService<IEvaluatorCatalog>(),
                    provider.GetRequiredService<IEvaluationResultStoreSelector>(),
                    provider.GetRequiredService<IEvaluationReportExporterCatalog>(),
                    provider.GetRequiredService<IIdentifierGenerator<EvaluationRunId>>(),
                    provider.GetRequiredService<TimeProvider>(),
                    EvaluationOptionsSnapshot.From(provider.GetRequiredService<IOptions<EvaluationOptions>>().Value),
                    provider.GetService<ILogger<EvaluationRunner>>() ?? NullLogger<EvaluationRunner>.Instance)
                : throw new InvalidOperationException(
                    $"The evaluation runner binds exactly one AgentEngine from its own service provider; {engines.Length} are registered.");
        });
        ThrowIfRunnerAmbiguous(services);
        return services;
    }

    /// <summary>Replaces the registered runner with one implementation.</summary>
    /// <typeparam name="TRunner">The runner implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    internal static IServiceCollection ReplaceRunner<TRunner>(IServiceCollection services)
        where TRunner : class, IEvaluationRunner
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.RemoveAll<IEvaluationRunner>();
        _ = services.AddSingleton<IEvaluationRunner, TRunner>();
        return services;
    }

    /// <summary>Adds one keyed evaluator, idempotently for an identical repeat.</summary>
    /// <typeparam name="TEvaluator">The evaluator implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="key">The stable evaluator key.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
    /// <exception cref="InvalidOperationException">The key is already registered for a different implementation.</exception>
    internal static IServiceCollection AddEvaluator<TEvaluator>(IServiceCollection services, EvaluatorKey key)
        where TEvaluator : class, IEvaluator
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        AddDefaults(services);
        AddKeyed<IEvaluator, TEvaluator>(services, key.Value, "evaluator");
        return services;
    }

    /// <summary>Replaces the evaluator registered under a key, or adds it when none is registered.</summary>
    /// <typeparam name="TEvaluator">The evaluator implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="key">The stable evaluator key.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
    internal static IServiceCollection ReplaceEvaluator<TEvaluator>(IServiceCollection services, EvaluatorKey key)
        where TEvaluator : class, IEvaluator
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        AddDefaults(services);
        _ = services.RemoveAllKeyed<IEvaluator>(key.Value);
        _ = services.AddKeyedSingleton<IEvaluator, TEvaluator>(key.Value);
        return services;
    }

    /// <summary>Adds one keyed result store, idempotently for an identical repeat.</summary>
    /// <typeparam name="TStore">The store implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="key">The store key a plan selects.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
    /// <exception cref="InvalidOperationException">The key is already registered for a different implementation.</exception>
    internal static IServiceCollection AddStore<TStore>(IServiceCollection services, EvaluationResultStoreKey key)
        where TStore : class, IEvaluationResultStore
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        AddDefaults(services);
        AddKeyed<IEvaluationResultStore, TStore>(services, key.Value, "result store");
        return services;
    }

    /// <summary>Adds one keyed report exporter, idempotently for an identical repeat.</summary>
    /// <typeparam name="TExporter">The exporter implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="key">The exporter key a plan names.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
    /// <exception cref="InvalidOperationException">The key is already registered for a different implementation.</exception>
    internal static IServiceCollection AddExporter<TExporter>(IServiceCollection services, EvaluationReportExporterKey key)
        where TExporter : class, IEvaluationReportExporter
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        AddDefaults(services);
        AddKeyed<IEvaluationReportExporter, TExporter>(services, key.Value, "report exporter");
        return services;
    }

    /// <summary>Registers the model judge evaluator under its documented key with explicit settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The judge configuration, applied to a fresh options object and validated now.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    /// <exception cref="ArgumentException">No judge model alias is configured.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured bound is out of range.</exception>
    /// <exception cref="InvalidOperationException">The key is already registered for a different judge configuration or evaluator.</exception>
    internal static IServiceCollection AddModelJudge(IServiceCollection services, Action<ModelJudgeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new ModelJudgeOptions();
        configure(options);
        var settings = new ModelJudgeSettings(
            options.JudgeModel,
            options.RepeatCount,
            new ModelJudgeBudget(options.MaximumCalls, options.MaximumTokens),
            options.MaximumStandardDeviation,
            options.MaximumCandidateCharacters);
        AddDefaults(services);
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(ModelJudgeRegistration) && descriptor.ImplementationInstance is ModelJudgeRegistration existing)
            {
                return existing.Settings == settings
                    ? services
                    : throw new InvalidOperationException(
                        "The model judge evaluator is already registered with different settings. Use ReplaceEvaluator for a deliberate replacement.");
            }
        }

        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(IEvaluator) && Equals(descriptor.ServiceKey, ModelJudgeEvaluator.Key.Value))
            {
                throw new InvalidOperationException(
                    $"The evaluator key '{ModelJudgeEvaluator.Key}' is already registered for a different implementation.");
            }
        }

        _ = services.AddSingleton(new ModelJudgeRegistration(settings));
        _ = services.AddKeyedSingleton<IEvaluator>(
            ModelJudgeEvaluator.Key.Value,
            (provider, _) => new ModelJudgeEvaluator(provider.GetRequiredService<IModelJudgeClient>(), settings));
        return services;
    }

    /// <summary>Registers the first-party judge client over the provider-neutral model services.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional client options, applied to a fresh options object and validated at construction.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    internal static IServiceCollection AddModelRequestJudgeClient(IServiceCollection services, Action<ModelRequestJudgeClientOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new ModelRequestJudgeClientOptions();
        configure?.Invoke(options);
        AddDefaults(services);
        services.TryAddSingleton<IIdentifierGenerator<ModelRequestId>, GuidModelRequestIdGenerator>();
        services.TryAddSingleton<IIdentifierGenerator<OperationId>, GuidOperationIdGenerator>();
        services.TryAddSingleton<IModelJudgeClient>(provider => new ModelRequestJudgeClient(
            provider.GetRequiredService<IModelCatalog>(),
            provider.GetRequiredService<IModelSelector>(),
            provider.GetRequiredService<ILlmModelResolver>(),
            provider.GetRequiredService<IIdentifierGenerator<ModelRequestId>>(),
            provider.GetRequiredService<IIdentifierGenerator<OperationId>>(),
            provider.GetRequiredService<TimeProvider>(),
            options));
        return services;
    }

    /// <summary>Registers the replaceable defaults every evaluation registration shares.</summary>
    /// <param name="services">The non-null service collection.</param>
    internal static void AddDefaults(IServiceCollection services)
    {
        Debug.Assert(services is not null, "Callers validate the service collection before adding defaults.");
        _ = services.AddAgentKitObservability();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIdentifierGenerator<EvaluationRunId>, GuidEvaluationRunIdGenerator>();
        services.TryAddSingleton<IEvaluatorCatalog>(static provider => new DefaultEvaluatorCatalog(provider));
        services.TryAddSingleton<IEvaluationResultStoreSelector>(static provider => new DefaultEvaluationResultStoreSelector(provider));
        services.TryAddSingleton<IEvaluationReportExporterCatalog>(static provider => new DefaultEvaluationReportExporterCatalog(provider));
    }

    private static void AddKeyed<TService, TImplementation>(IServiceCollection services, string key, string description)
        where TService : class
        where TImplementation : class, TService
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(key), "Callers validate the key before adding a keyed registration.");
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType != typeof(TService) || !Equals(descriptor.ServiceKey, key))
            {
                continue;
            }

            if (descriptor.KeyedImplementationType == typeof(TImplementation))
            {
                return;
            }

            throw new InvalidOperationException(
                $"The {description} key '{key}' is already registered for a different implementation. Use a distinct key or replace the registration deliberately.");
        }

        _ = services.AddKeyedSingleton<TService, TImplementation>(key);
    }

    private static void ThrowIfRunnerAmbiguous(IServiceCollection services)
    {
        var runners = 0;
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(IEvaluationRunner) && !descriptor.IsKeyedService)
            {
                runners++;
            }
        }

        if (runners > 1)
        {
            throw new InvalidOperationException(
                "More than one IEvaluationRunner is registered. Use ReplaceEvaluationRunner to select exactly one.");
        }
    }
}
