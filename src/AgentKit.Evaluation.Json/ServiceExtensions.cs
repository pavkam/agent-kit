// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Json;

/// <summary>Registers durable inspectable host-local JSON evaluation result storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed JSON evaluation result store over an explicit host-authorized root.</summary>
        /// <param name="key">The registration key a plan selects; repeated registration of the same key is ignored.</param>
        /// <param name="target">The exact root and bootstrap effects the host authorizes; it must not be shared with another store.</param>
        /// <param name="configure">Optional bounds and encoding contract.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is not positive.</exception>
        /// <remarks>The store is a singleton. It opens its root lazily on first use; call <see cref="JsonEvaluationResultStore.InitializeAsync"/> at host startup to surface a misconfigured root early. No persistence target is ever chosen implicitly.</remarks>
        public IServiceCollection AddJsonEvaluationResultStore(EvaluationResultStoreKey key, JsonEvaluationStoreTarget target, Action<JsonEvaluationStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            var options = new JsonEvaluationStoreOptions();
            configure?.Invoke(options);
            var settings = new JsonEvaluationStoreSettings(options.MaximumRecordBytes, options.MaximumDocumentBytes, options.Encoding);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddKeyedSingleton<IEvaluationResultStore>(key.Value, (provider, _) => new JsonEvaluationResultStore(
                target,
                settings,
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<JsonEvaluationResultStore>>()));
            return services;
        }
    }
}
