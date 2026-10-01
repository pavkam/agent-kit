// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.InMemory;

/// <summary>Registers explicitly ephemeral process-local goal storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed in-memory goal store.</summary>
        /// <param name="key">The registration key a goal profile selects; repeated registration of the same key is ignored.</param>
        /// <param name="configure">Optional store options, applied when the store is first constructed.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        /// <remarks>The store is a singleton that requires an <see cref="ISecurityGrantStore"/> and never chooses itself as a persistence target: selecting it is the application's explicit act of accepting ephemeral goals.</remarks>
        public IServiceCollection AddInMemoryGoalStore(GoalStoreKey key, Action<InMemoryGoalStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();
            services.TryAddKeyedSingleton<IGoalStore>(key.Value, (provider, _) =>
            {
                var options = new InMemoryGoalStoreOptions();
                configure?.Invoke(options);
                return new InMemoryGoalStore(
                    provider.GetRequiredService<ISecurityGrantStore>(),
                    provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                    provider.GetRequiredService<TimeProvider>(),
                    options,
                    provider.GetService<ILogger<InMemoryGoalStore>>());
            });
            return services;
        }
    }
}
