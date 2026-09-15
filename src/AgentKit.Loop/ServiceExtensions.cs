// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration for the built-in, reduced-scope agent
/// loop.
/// </summary>
/// <remarks>
/// Composition requires an <see cref="ISessionCoordinator"/>, an
/// <see cref="IContextAssembler"/>, an <see cref="IToolInvoker"/>, and at
/// least one <see cref="ILlmModel"/> to already be registered; this method
/// does not register any of them itself. Every collaborator the loop drives
/// a run with — including those — arrives per run through the compiled
/// <see cref="AgentRunServices"/> bundle rather than through this
/// registration.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the built-in <see cref="DefaultAgentLoop"/> as a keyed, scoped <see cref="IAgentLoop"/>.</summary>
        /// <param name="key">
        /// The stable key this loop registration is selected by. An agent definition that leaves
        /// <see cref="AgentDefinition.LoopKey"/> unset resolves to <see cref="AgentLoopComponentDefaults.LoopKey"/>,
        /// so passing that same value here registers exactly the loop every otherwise-unconfigured definition uses.
        /// </param>
        /// <param name="configure">Optional configuration for this key's <see cref="AgentLoopOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is uninitialized.</exception>
        /// <exception cref="InvalidOperationException">A different <see cref="IAgentLoop"/> implementation is already registered under <paramref name="key"/>.</exception>
        /// <remarks>
        /// <para>
        /// Uses <c>TryAddKeyedScoped</c> semantics: repeated equivalent calls for the same key are idempotent, and
        /// a conflicting implementation already registered under that key is diagnosed rather than silently
        /// replaced. <see cref="ReplaceAgentLoop{TLoop}(IServiceCollection, ComponentKey{IAgentLoop})"/> is the
        /// explicit replacement path. The registered loop is scoped: one instance serves exactly one run scope.
        /// </para>
        /// <para>
        /// The loop consults the <see cref="IRunContinuationPolicy"/> registered under
        /// <see cref="AgentLoopDefaults.ContinuationPolicyKey"/> after every committed turn; registering a custom
        /// policy under that key before calling this method replaces the built-in
        /// <see cref="DefaultRunContinuationPolicy"/>.
        /// </para>
        /// </remarks>
        public IServiceCollection AddAgentLoop(
            ComponentKey<IAgentLoop> key,
            Action<AgentLoopOptions>? configure = null) =>
            AgentLoopRegistration.AddDefault(services, key, configure);

        /// <summary>Additively registers a custom keyed, scoped <see cref="IAgentLoop"/> implementation.</summary>
        /// <typeparam name="TLoop">The scoped loop implementation.</typeparam>
        /// <param name="key">The stable key this loop registration is selected by.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is uninitialized.</exception>
        /// <exception cref="InvalidOperationException">A different <see cref="IAgentLoop"/> implementation is already registered under <paramref name="key"/>.</exception>
        public IServiceCollection AddAgentLoop<TLoop>(ComponentKey<IAgentLoop> key)
            where TLoop : class, IAgentLoop =>
            AgentLoopRegistration.Add<TLoop>(services, key);

        /// <summary>Replaces whatever <see cref="IAgentLoop"/> is registered under a key with a new implementation.</summary>
        /// <typeparam name="TLoop">The scoped replacement implementation.</typeparam>
        /// <param name="key">The stable key whose registration is replaced.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is uninitialized.</exception>
        public IServiceCollection ReplaceAgentLoop<TLoop>(ComponentKey<IAgentLoop> key)
            where TLoop : class, IAgentLoop =>
            AgentLoopRegistration.Replace<TLoop>(services, key);

        /// <summary>Additively registers a singleton continuation policy under an explicit key.</summary>
        /// <typeparam name="TPolicy">The stateless, thread-safe policy implementation.</typeparam>
        /// <param name="key">The stable policy key selected by an agent definition.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is uninitialized.</exception>
        public IServiceCollection AddRunContinuationPolicy<TPolicy>(ComponentKey<IRunContinuationPolicy> key)
            where TPolicy : class, IRunContinuationPolicy =>
            AgentLoopRegistration.AddContinuationPolicy<TPolicy>(services, key);
    }
}
