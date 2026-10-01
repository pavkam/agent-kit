// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.InMemory;

/// <summary>Registers ephemeral in-process evaluation result storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed in-memory evaluation result store.</summary>
        /// <param name="key">The registration key a plan selects; repeated registration of the same key is ignored.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <remarks>The store is a singleton, is ephemeral, and is never selected implicitly. Results are lost when the host disposes the provider.</remarks>
        public IServiceCollection AddInMemoryEvaluationResultStore(EvaluationResultStoreKey key)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddKeyedSingleton<IEvaluationResultStore>(key.Value, (provider, _) => new InMemoryEvaluationResultStore(
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<InMemoryEvaluationResultStore>>()));
            return services;
        }
    }
}
