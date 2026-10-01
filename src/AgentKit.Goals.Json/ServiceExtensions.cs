// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Json;

/// <summary>Registers durable inspectable host-local JSON goal storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed JSON goal store over an explicit host-authorized root.</summary>
        /// <param name="key">The registration key a goal profile selects; repeated registration of the same key is ignored.</param>
        /// <param name="target">The exact root and bootstrap effects the host authorizes.</param>
        /// <param name="configure">Optional bounds, scanners, and encoding contract.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is not positive.</exception>
        /// <remarks>The store is a singleton that requires an <see cref="ISecurityGrantStore"/>. It opens its root lazily on first use; call <see cref="JsonGoalStore.InitializeAsync"/> at host startup to surface a misconfigured root early. No persistence target is ever chosen implicitly.</remarks>
        public IServiceCollection AddJsonGoalStore(GoalStoreKey key, JsonGoalStoreTarget target, Action<JsonGoalStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            var options = new JsonGoalStoreOptions();
            configure?.Invoke(options);
            var settings = new JsonGoalStoreSettings(
                options.MaximumRecordBytes,
                options.MaximumDocumentBytes,
                options.CompactionRecordThreshold,
                options.AuthorizedIntentScanners,
                options.Encoding);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();
            services.TryAddKeyedSingleton<IGoalStore>(key.Value, (provider, _) => new JsonGoalStore(
                target,
                settings,
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<JsonGoalStore>>()));
            return services;
        }
    }
}
