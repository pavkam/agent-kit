// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

using AgentKit.Observability;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency-injection registration for the built-in, reduced-scope agent
/// loop.
/// </summary>
/// <remarks>
/// Composition requires an <see cref="ISessionCoordinator"/>, an
/// <see cref="IContextAssembler"/>, an <see cref="IToolInvoker"/>, and at
/// least one <see cref="ILlmModel"/> to already be registered; this method
/// does not register any of them itself.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the built-in <see cref="DefaultAgentLoop"/>.</summary>
        /// <param name="configure">Optional configuration for <see cref="AgentLoopOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Idempotent: every registration here uses <c>TryAdd</c> semantics,
        /// so calling this more than once keeps the first registration. The
        /// loop consults the <see cref="IRunContinuationPolicy"/> registered
        /// under <see cref="AgentLoopDefaults.ContinuationPolicyKey"/> after
        /// every committed turn; registering a custom policy under that key
        /// before calling this method replaces the built-in
        /// <see cref="DefaultRunContinuationPolicy"/>.
        /// </remarks>
        public IServiceCollection AddAgentLoop(Action<AgentLoopOptions>? configure = null)
        {
            _ = services.AddAgentKitObservability();
            var optionsBuilder = services.AddOptions<AgentLoopOptions>()
                .Validate(o => o.HistoryReadPageSize > 0, "HistoryReadPageSize must be positive.");

            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

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
            services.TryAddSingleton<IAgentLoop, DefaultAgentLoop>();
            services.TryAddKeyedSingleton<IRunContinuationPolicy, DefaultRunContinuationPolicy>(
                AgentLoopDefaults.ContinuationPolicyKey.Value);

            return services;
        }

        /// <summary>Additively registers a singleton continuation policy under an explicit key.</summary>
        /// <typeparam name="TPolicy">The stateless, thread-safe policy implementation.</typeparam>
        /// <param name="key">The stable policy key selected by an agent definition.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is uninitialized.</exception>
        public IServiceCollection AddRunContinuationPolicy<TPolicy>(ComponentKey<IRunContinuationPolicy> key)
            where TPolicy : class, IRunContinuationPolicy
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            _ = services.AddKeyedSingleton<IRunContinuationPolicy, TPolicy>(key.Value);
            return services;
        }
    }
}
