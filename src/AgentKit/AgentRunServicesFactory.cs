// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Compiles the immutable <see cref="AgentRunServices"/> bundle a run's selected <see cref="IAgentLoop"/> drives with.</summary>
/// <remarks>
/// <para>
/// This is the run-activation boundary the agent-runtime architecture describes: it runs once per run, inside
/// the freshly created run scope, after the run's keyed <see cref="IAgentLoop"/> has been resolved. For every
/// collaborator that a first-party package could plausibly register under the same key as a specific loop
/// selection (session coordination, authorization capture, context assembly, tool invocation, model selection,
/// and model resolution), this factory prefers a registration keyed to that exact loop key and falls back to
/// the engine-wide unkeyed registration when no such keyed variant exists. This is what makes it possible for a
/// host to give one keyed loop selection its own collaborator without disturbing every other agent definition
/// that shares the engine-wide default — the defect this factory exists to close, since resolving these
/// collaborators through the loop's own constructor can only ever see one, permanently unkeyed, registration.
/// </para>
/// <para>
/// <see cref="IModelCatalog"/> is always resolved unkeyed: composition requires exactly one engine-wide model
/// catalog (see the composition-validation rules in the repository's architectural guidance), so per-loop
/// catalog selection is not a supported axis and this factory never looks for a keyed variant of it.
/// </para>
/// <para>
/// The continuation policy is resolved from the fixed, well-known key
/// <see cref="AgentLoopComponentDefaults.ContinuationPolicyKey"/> rather than from the run's loop key: today's
/// reduced loop consults one replaceable, engine-wide policy rather than a policy selected per keyed loop.
/// </para>
/// <para>
/// The output processor, the compactor, the budget authority, the run coordinator, the input coordinator, and
/// the output publisher are all optional and resolved unkeyed from the run scope: <c>AddAgentOutput</c> forwards
/// the default keyed processor to that unkeyed registration, and <c>AddContextCompaction</c> registers the
/// compactor. A definition that selects an output contract without a composed processor fails closed inside the
/// loop rather than at compilation; a composition without a compactor simply never compacts. The input
/// coordinator and output publisher are resolved unkeyed even though <c>AgentDefinition</c> carries
/// <c>InputCoordinatorKey</c> and <c>OutputPublisherKey</c> for <c>AgentIORegistration</c>'s own keyed
/// registrations — routing this factory's resolution through those keys is a known follow-up, not yet needed
/// while most compositions register exactly one of each.
/// </para>
/// </remarks>
internal static class AgentRunServicesFactory
{
    /// <summary>Compiles the per-run collaborator bundle for one keyed loop selection.</summary>
    /// <param name="provider">The run's scoped service provider.</param>
    /// <param name="loopKey">The exact key selected for this run's <see cref="IAgentLoop"/>.</param>
    /// <returns>The compiled, immutable bundle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="loopKey"/> is uninitialized.</exception>
    /// <exception cref="InvalidOperationException">A required collaborator has neither a keyed nor an unkeyed registration.</exception>
    internal static AgentRunServices Compile(IServiceProvider provider, ComponentKey<IAgentLoop> loopKey)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(loopKey.Value, nameof(loopKey));

        var key = loopKey.Value;
        return new AgentRunServices(
            ResolveKeyedOrShared<ISessionCoordinator>(provider, key),
            ResolveKeyedOrShared<ISecurityProfileSelector>(provider, key),
            ResolveKeyedOrShared<IContextAssembler>(provider, key),
            ResolveKeyedOrShared<IToolInvoker>(provider, key),
            provider.GetRequiredService<IModelCatalog>(),
            ResolveKeyedOrShared<IModelSelector>(provider, key),
            ResolveKeyedOrShared<ILlmModelResolver>(provider, key),
            provider.GetRequiredKeyedService<IRunContinuationPolicy>(
                AgentLoopComponentDefaults.ContinuationPolicyKey.Value),
            provider.GetService<IOutputProcessor>(),
            provider.GetService<ICompactor>(),
            provider.GetService<IBudgetAuthority>(),
            provider.GetService<ISessionRunCoordinator>(),
            provider.GetService<IInputCoordinator>(),
            provider.GetService<IOutputPublisher>());
    }

    /// <summary>Resolves a collaborator keyed to the run's exact loop selection, falling back to the engine-wide unkeyed registration.</summary>
    /// <typeparam name="TService">The collaborator contract.</typeparam>
    /// <param name="provider">The run's scoped service provider.</param>
    /// <param name="key">The exact loop key this run selected.</param>
    /// <returns>The keyed registration for <paramref name="key"/> when one exists; otherwise the unkeyed registration.</returns>
    /// <exception cref="InvalidOperationException">Neither a keyed nor an unkeyed registration exists.</exception>
    /// <remarks>
    /// Exposed at assembly visibility so <see cref="AgentEngine"/> can resolve the exact same
    /// <see cref="ISessionCoordinator"/> instance this factory would compile into <see cref="AgentRunServices"/>
    /// before the full bundle is compiled — specifically to build <see cref="SessionExecutionCapability"/> and
    /// populate <see cref="RunScopeState"/> ahead of resolving any scoped collaborator that depends on it.
    /// </remarks>
    internal static TService ResolveKeyedOrShared<TService>(IServiceProvider provider, string key)
        where TService : class
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(key), "A validated loop key is required to resolve a per-run collaborator.");
        return provider.GetKeyedService<TService>(key) ?? provider.GetRequiredService<TService>();
    }
}
