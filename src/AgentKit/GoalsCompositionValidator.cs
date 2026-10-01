// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Validates that every agent selecting a goal profile can actually own and delegate goals.</summary>
/// <remarks>
/// <para>
/// Goals and delegation are an optional capability. An agent that names no goal profile is never diagnosed here, and a composition
/// with no such agent needs no goal registration at all. Naming a profile, however, states that this agent's delegated work is
/// recorded as durable goals, so composition must prove the whole path exists rather than discovering a missing store or dispatcher
/// after a child was already authorized.
/// </para>
/// <para>
/// Validation reads build-local registration descriptors and the published profile catalog only. It never activates a store, dispatcher,
/// or worker, because activating a persistence adapter during validation would perform exactly the protected effects the composition
/// has not yet been proven able to authorize. Delegation policy identities are resolved by the pipeline when a delegation is evaluated
/// and fail closed there; composition proves the registrations a profile names, not the behavior of a policy.
/// </para>
/// </remarks>
internal static class GoalsCompositionValidator
{
    /// <summary>Diagnoses every goal gap across the definitions that select a goal profile.</summary>
    /// <param name="definitions">The published agent definitions this composition can run.</param>
    /// <param name="profileCatalog">The registered goal profile catalog, or null when none is registered.</param>
    /// <param name="componentRegistrations">The non-null build-local registration evidence.</param>
    /// <param name="diagnostics">The accumulating diagnostic builder this validator appends to.</param>
    /// <remarks>Each definition reports at most one missing-profile diagnostic: without a resolved profile snapshot there are no keys to check, so continuing would produce noise rather than information.</remarks>
    internal static void Validate(
        ImmutableArray<AgentDefinition> definitions,
        IGoalProfileCatalog? profileCatalog,
        ComponentRegistrationSnapshot componentRegistrations,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(!definitions.IsDefault, "Goal validation runs only over a materialized definition set.");
        Debug.Assert(componentRegistrations is not null, "Goal validation requires registration evidence.");
        Debug.Assert(diagnostics is not null, "Goal validation appends to a caller-owned builder.");
        var validatedEngineWide = false;
        var validatedProfiles = new HashSet<GoalProfileKey>();
        foreach (var definition in definitions)
        {
            if (definition.OptionalCapabilities.GoalProfile is not { } profileKey)
            {
                continue;
            }

            if (!validatedEngineWide)
            {
                ValidateEngineWide(componentRegistrations, diagnostics);
                validatedEngineWide = true;
            }

            if (profileCatalog is null || !profileCatalog.TryGet(profileKey, out var profile))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.goals.profile.missing",
                    $"Agent '{definition.Id}' selects goal profile '{profileKey.Value}' but it is not registered."));
                continue;
            }

            if (!validatedProfiles.Add(profileKey))
            {
                continue;
            }

            RequireKeyed<IGoalStore>(componentRegistrations, profile.StoreKey.Value, definition.Id, profileKey, "agentkit.goals.store.missing", diagnostics);
            RequireKeyed<IDelegationDispatcher>(componentRegistrations, profile.DispatcherKey.Value, definition.Id, profileKey, "agentkit.goals.dispatcher.missing", diagnostics);
            foreach (var strategy in profile.JoinStrategies)
            {
                RequireKeyed<IGoalJoinStrategy>(componentRegistrations, strategy.Value, definition.Id, profileKey, "agentkit.goals.join-strategy.missing", diagnostics);
            }
        }
    }

    /// <summary>Requires the singular goal runtime services every goal-owning agent shares.</summary>
    /// <param name="componentRegistrations">The build-local registration evidence.</param>
    /// <param name="diagnostics">The accumulating diagnostic builder.</param>
    /// <remarks>These are engine-wide rather than per definition, so they are diagnosed once even when several definitions select a goal profile.</remarks>
    private static void ValidateEngineWide(ComponentRegistrationSnapshot componentRegistrations, ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(componentRegistrations is not null, "Engine-wide validation requires registration evidence.");
        RequireUnkeyed<IGoalCoordinator>(componentRegistrations, "agentkit.goals.coordinator.missing", diagnostics);
        RequireUnkeyed<IDelegationCoordinator>(componentRegistrations, "agentkit.goals.delegation-coordinator.missing", diagnostics);
        RequireUnkeyed<IGoalStoreSelector>(componentRegistrations, "agentkit.goals.store-selector.missing", diagnostics);
        RequireUnkeyed<IDelegationDispatcherSelector>(componentRegistrations, "agentkit.goals.dispatcher-selector.missing", diagnostics);
        RequireUnkeyed<IGoalJoinStrategySelector>(componentRegistrations, "agentkit.goals.join-selector.missing", diagnostics);
        RequireUnkeyed<IDelegationTargetCatalog>(componentRegistrations, "agentkit.goals.target-catalog.missing", diagnostics);
        RequireUnkeyed<IDelegationTargetSelector>(componentRegistrations, "agentkit.goals.target-selector.missing", diagnostics);
        RequireUnkeyed<IDelegationPolicyPipeline>(componentRegistrations, "agentkit.goals.policy-pipeline.missing", diagnostics);
        RequireUnkeyed<IGoalBudgetManager>(componentRegistrations, "agentkit.goals.budget-manager.missing", diagnostics);
        RequireUnkeyed<IGoalEventDispatcher>(componentRegistrations, "agentkit.goals.event-dispatcher.missing", diagnostics);
        RequireUnkeyed<IGoalProfileCatalog>(componentRegistrations, "agentkit.goals.profile-catalog.missing", diagnostics);
    }

    private static void RequireUnkeyed<TService>(ComponentRegistrationSnapshot componentRegistrations, string code, ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
        where TService : class
    {
        var registered = componentRegistrations.Services.Any(static service => !service.IsKeyedService && service.ServiceType == typeof(TService));
        if (!registered)
        {
            diagnostics.Add(new CompositionDiagnostic(
                code,
                $"A goal profile is selected but no {typeof(TService).Name} is registered. Call AddAgentGoals."));
        }
    }

    /// <summary>Requires an exactly keyed registration for one component the profile names.</summary>
    /// <typeparam name="TService">The keyed goal contract.</typeparam>
    /// <remarks>There is deliberately no unkeyed fallback: a goal persists the profile it was created under, so a component must be resolvable by exactly the key the profile names.</remarks>
    private static void RequireKeyed<TService>(
        ComponentRegistrationSnapshot componentRegistrations,
        string key,
        AgentId agentId,
        GoalProfileKey profileKey,
        string code,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
        where TService : class
    {
        var registered = componentRegistrations.Services.Any(service =>
            service.IsKeyedService
            && service.ServiceType == typeof(TService)
            && key.Equals(service.ServiceKey as string, StringComparison.Ordinal));
        if (!registered)
        {
            diagnostics.Add(new CompositionDiagnostic(
                code,
                $"Agent '{agentId}' selects goal profile '{profileKey.Value}', which names {typeof(TService).Name} key '{key}', but no keyed registration exists for it."));
        }
    }
}
