// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting;

/// <summary>Registers the host worker that drains delegation intents without selecting a store or profile for the application.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the delegation worker, its wake-up queue, its slot pool, and the engine-backed child runner.</summary>
        /// <param name="configure">Optional worker bounds and the profiles to scan.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// <para>
        /// Registration is idempotent: a repeat adds only further <paramref name="configure"/> callbacks. The queue becomes the
        /// intent signal and the slot pool becomes the wait parking, replacing the goal runtime's defaults; the worker is added
        /// once as a hosted service. A host starts it at startup; a standalone engine has no host, so the first committed intent
        /// starts it and disposing the engine's provider stops it. The worker resolves the engine lazily, so it never constructs
        /// the engine during composition.
        /// </para>
        /// <para>Register it after <c>AddAgentGoals</c> and a goal store, and replace the child runner with <c>ReplaceDelegationChildRunner</c> to run children elsewhere.</para>
        /// </remarks>
        public IServiceCollection AddGoalDelegationWorker(Action<GoalWorkerOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var builder = services.AddOptions<GoalWorkerOptions>()
                .Validate(static options => options.MaximumConcurrentChildren > 0, "MaximumConcurrentChildren must be positive.")
                .Validate(static options => options.QueueCapacity > 0, "QueueCapacity must be positive.")
                .Validate(static options => options.ScanInterval > TimeSpan.Zero, "ScanInterval must be positive.")
                .Validate(static options => options.ScanPageSize > 0, "ScanPageSize must be positive.")
                .Validate(static options => options.MaximumSummaryCharacters > 0, "MaximumSummaryCharacters must be positive.")
                .Validate(static options => !string.IsNullOrWhiteSpace(options.ScannerId.Value), "ScannerId must be set.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = builder.Configure(configure);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(static provider => new DelegationIntentQueue(
                provider.GetRequiredService<IOptions<GoalWorkerOptions>>(),
                () => provider.GetRequiredService<GoalDelegationWorker>().StartAsync(CancellationToken.None)));
            services.TryAddSingleton<DelegationWorkerSlots>();
            services.TryAddSingleton<IDelegationChildRunner, EngineDelegationChildRunner>();
            services.TryAddSingleton<DelegationIntentProcessor>();
            _ = services.Replace(ServiceDescriptor.Singleton<IDelegationIntentSignal>(static provider => provider.GetRequiredService<DelegationIntentQueue>()));
            _ = services.Replace(ServiceDescriptor.Singleton<IDelegationWaitParking>(static provider => provider.GetRequiredService<DelegationWorkerSlots>()));
            if (!services.Any(static descriptor => descriptor.ServiceType == typeof(GoalDelegationWorker)))
            {
                _ = services.AddSingleton(static provider => new GoalDelegationWorker(
                    provider.GetRequiredService<DelegationIntentQueue>(),
                    provider.GetRequiredService<DelegationIntentProcessor>(),
                    provider.GetRequiredService<IGoalStoreSelector>(),
                    provider.GetRequiredService<DelegationWorkerSlots>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<IOptions<GoalWorkerOptions>>(),
                    provider.GetService<ILogger<GoalDelegationWorker>>()));
                _ = services.AddSingleton<IHostedService>(static provider => provider.GetRequiredService<GoalDelegationWorker>());
            }

            return services;
        }

        /// <summary>Registers the engine-backed agent-to-agent message channel.</summary>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>Idempotent (<c>TryAdd</c>). The channel admits each message through the recipient's public input path, so the composition needs the same input coordinator any other input needs, and it resolves the engine lazily.</remarks>
        public IServiceCollection AddAgentMessageChannel()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton<IAgentMessageChannel, EngineAgentMessageChannel>();
            return services;
        }

        /// <summary>Replaces the child runner that provisions sessions and executes claimed attempts.</summary>
        /// <typeparam name="TRunner">The replacement runner type, constructed by the container as a singleton.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceDelegationChildRunner<TRunner>()
            where TRunner : class, IDelegationChildRunner
        {
            ArgumentNullException.ThrowIfNull(services);
            return services.Replace(ServiceDescriptor.Singleton<IDelegationChildRunner, TRunner>());
        }
    }
}
