// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Validates that every agent selecting a durability profile can actually run durably.</summary>
/// <remarks>
/// <para>
/// Durable execution is an optional capability. An agent that names no durability profile is never diagnosed here,
/// and a composition with no durable agent needs no durability registration at all. Naming a profile, however, states
/// that this agent's work must survive process loss, so composition must prove the whole path exists rather than
/// discovering a missing journal after an effect has already run.
/// </para>
/// <para>
/// Validation reads build-local registration descriptors and the profile catalog only. It never activates a journal,
/// lease manager, or backend, because activating a persistence adapter during validation would perform exactly the
/// protected effects the composition has not yet been proven able to authorize.
/// </para>
/// </remarks>
internal static class DurabilityCompositionValidator
{
    /// <summary>Diagnoses every durability gap across the definitions that select a durability profile.</summary>
    /// <param name="definitions">The published agent definitions this composition can run.</param>
    /// <param name="profileCatalog">The registered durability profile catalog, or null when none is registered.</param>
    /// <param name="componentRegistrations">The non-null build-local registration evidence.</param>
    /// <param name="operationHandlers">
    /// The non-null registered durable operation handlers, resolved only when a definition selects durability. A
    /// profile that enables an operation name none of them owns is diagnosed.
    /// </param>
    /// <param name="diagnostics">The accumulating diagnostic builder this validator appends to.</param>
    /// <remarks>
    /// Each definition reports at most one missing-profile diagnostic: without a resolved profile snapshot there are
    /// no component keys to check, so continuing would produce noise rather than information.
    /// </remarks>
    internal static void Validate(
        ImmutableArray<AgentDefinition> definitions,
        IDurabilityProfileCatalog? profileCatalog,
        ComponentRegistrationSnapshot componentRegistrations,
        IReadOnlyCollection<IDurableOperationHandler> operationHandlers,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(!definitions.IsDefault, "Durability validation runs only over a materialized definition set.");
        Debug.Assert(componentRegistrations is not null, "Durability validation requires registration evidence.");
        Debug.Assert(operationHandlers is not null, "Durability validation requires the registered handlers.");
        Debug.Assert(diagnostics is not null, "Durability validation appends to a caller-owned builder.");
        var handledNames = operationHandlers.Select(static handler => handler.OperationName).ToHashSet();

        var validatedEngineWide = false;
        foreach (var definition in definitions)
        {
            if (definition.OptionalCapabilities.DurabilityProfile is not { } profileKey)
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
                    "agentkit.durability.profile.missing",
                    $"Agent '{definition.Id}' selects durability profile '{profileKey.Value}' but it is not registered."));
                continue;
            }

            RequireKeyed<IDurableOperationJournal>(
                componentRegistrations,
                profile.JournalKey.Value,
                definition.Id,
                profileKey,
                "agentkit.durability.journal.missing",
                diagnostics);
            RequireKeyed<IDurableLeaseManager>(
                componentRegistrations,
                profile.LeaseManagerKey.Value,
                definition.Id,
                profileKey,
                "agentkit.durability.lease-manager.missing",
                diagnostics);
            RequireKeyed<IRecoveryPolicy>(
                componentRegistrations,
                profile.RecoveryPolicyKey.Value,
                definition.Id,
                profileKey,
                "agentkit.durability.recovery-policy.missing",
                diagnostics);
            RequireKeyed<IDurableExecutionBackend>(
                componentRegistrations,
                profile.BackendKey.Value,
                definition.Id,
                profileKey,
                "agentkit.durability.backend.missing",
                diagnostics);

            // A profile that lists a boundary promises evidence for it, and a boundary with no handler can never be
            // journaled: the coordinator refuses it in the middle of a run. Reporting it here keeps that promise
            // honest at composition instead of discovering it as a silently missing record.
            foreach (var operation in profile.EnabledOperations.Where(name => !handledNames.Contains(name)))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.durability.handler.missing",
                    $"Durability profile '{profileKey.Value}' enables operation '{operation.Value}' but no durable "
                    + "operation handler is registered for it. Register the package that owns the operation or remove "
                    + "it from the profile."));
            }
        }
    }

    /// <summary>Requires the singular durability runtime services every durable agent shares.</summary>
    /// <param name="componentRegistrations">The build-local registration evidence.</param>
    /// <param name="diagnostics">The accumulating diagnostic builder.</param>
    /// <remarks>
    /// These are engine-wide rather than per definition, so they are diagnosed once even when several definitions
    /// select durability. The identifier generators are included because a coordinator that cannot mint a checkpoint
    /// or worker identity cannot record ownership at all.
    /// </remarks>
    private static void ValidateEngineWide(
        ComponentRegistrationSnapshot componentRegistrations,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(componentRegistrations is not null, "Engine-wide validation requires registration evidence.");
        RequireUnkeyed<IDurableExecutionCoordinator>(
            componentRegistrations, "agentkit.durability.coordinator.missing", diagnostics);
        RequireUnkeyed<IDurabilityRuntimeSelector>(
            componentRegistrations, "agentkit.durability.runtime-selector.missing", diagnostics);
        RequireUnkeyed<IDurableBackendCatalog>(
            componentRegistrations, "agentkit.durability.backend-catalog.missing", diagnostics);
        RequireUnkeyed<IDurableExecutionEventDispatcher>(
            componentRegistrations, "agentkit.durability.event-dispatcher.missing", diagnostics);
        RequireUnkeyed<IIdentifierGenerator<CheckpointId>>(
            componentRegistrations, "agentkit.durability.checkpointid.missing", diagnostics);
        RequireUnkeyed<IIdentifierGenerator<WorkerId>>(
            componentRegistrations, "agentkit.durability.workerid.missing", diagnostics);
    }

    private static void RequireUnkeyed<TService>(
        ComponentRegistrationSnapshot componentRegistrations,
        string code,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
        where TService : class
    {
        var registered = componentRegistrations.Services.Any(static service =>
            !service.IsKeyedService && service.ServiceType == typeof(TService));
        if (!registered)
        {
            diagnostics.Add(new CompositionDiagnostic(
                code,
                $"A durability profile is selected but no {typeof(TService).Name} is registered. Call AddAgentDurability."));
        }
    }

    /// <summary>Requires an exactly keyed registration for one component the profile names.</summary>
    /// <typeparam name="TService">The keyed durability contract.</typeparam>
    /// <remarks>
    /// There is deliberately no unkeyed fallback. A durable operation persists the exact key it ran under so recovery
    /// activates that same composition; accepting an unkeyed registration here would let recovery resolve a component
    /// the operation never used.
    /// </remarks>
    private static void RequireKeyed<TService>(
        ComponentRegistrationSnapshot componentRegistrations,
        string key,
        AgentId agentId,
        DurabilityProfileKey profileKey,
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
                $"Agent '{agentId}' selects durability profile '{profileKey.Value}', which names {typeof(TService).Name} key '{key}', but no keyed registration exists for it."));
        }
    }
}
