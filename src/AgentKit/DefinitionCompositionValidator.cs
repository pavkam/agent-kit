// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Validates every published <see cref="AgentDefinition"/> against the registrations, profiles, and optional
/// capabilities it selects.
/// </summary>
/// <remarks>
/// <para>
/// The validator is the per-definition half of composition validation; <see cref="AgentCompositionValidator"/> owns
/// the engine-wide singular checks. Every required selection in <see cref="AgentComponentSelection"/> must resolve to
/// exactly one registration under the exact contract and key the definition names: an unkeyed registration never
/// satisfies a keyed selection, so one definition's key cannot silently bind another definition's collaborator.
/// </para>
/// <para>
/// Every problem is collected rather than thrown on first failure. Checks run over materialized snapshots and
/// registration descriptors only; no factory is invoked, and no remote service is probed.
/// </para>
/// </remarks>
internal static class DefinitionCompositionValidator
{
    /// <summary>Validates the published catalog, its run-profile publications, and every definition's selections.</summary>
    /// <param name="catalog">The non-null catalog whose current snapshot is validated.</param>
    /// <param name="profileReader">The run-profile publication reader, or null when unregistered.</param>
    /// <param name="registrations">The non-null frozen registrations and declarations.</param>
    /// <param name="services">The non-null optional engine services consulted per definition.</param>
    /// <param name="diagnostics">The initialized collector for every deterministic problem found.</param>
    /// <returns>The validated run-profile snapshot, or null when validation could not reach per-definition checks.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    internal static AgentRunProfilePublicationSnapshot? Validate(
        IAgentDefinitionCatalog catalog,
        IAgentRunProfilePublicationReader? profileReader,
        ComponentRegistrationSnapshot registrations,
        DefinitionValidationServices services,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var snapshot = catalog.CurrentSnapshot;
        if (snapshot is null)
        {
            diagnostics.Add(new CompositionDiagnostic("agentkit.catalog.not-ready", "The agent definition catalog has no materialized bootstrap snapshot."));
            return null;
        }

        if (snapshot.Definitions.IsEmpty)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.catalog.empty",
                "No agent definition is published. Register at least one with AddAgent."));
            return null;
        }

        if (profileReader?.CurrentSnapshot is not { } profileSnapshot)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.run-profile.not-ready",
                "The run-profile publication reader has no materialized bootstrap snapshot."));
            return null;
        }

        var publications = new Dictionary<(AgentId, AgentDefinitionRevision), AgentRunProfilePublication>();
        foreach (var publication in profileSnapshot.Publications)
        {
            var key = (publication.SecurityProfile.AgentId, publication.SecurityProfile.AgentDefinitionRevision);
            if (!publications.TryAdd(key, publication))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.run-profile.duplicate",
                    "More than one run-profile publication uses the same agent and definition revision."));
            }
        }

        ModelCatalogSnapshot? modelSnapshot = null;
        var modelSnapshotFailed = false;
        foreach (var definition in snapshot.Definitions)
        {
            if (!publications.TryGetValue((definition.Id, definition.Revision), out var publication))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.run-profile.missing",
                    $"Agent '{definition.Id}' has no exact run-profile publication for revision {definition.Revision}."));
                continue;
            }

            if (publication.SecurityProfile.ProfileKey != definition.SecurityProfile
                || publication.SessionProfile.Reference.Key != definition.SessionProfile
                || publication.HookProfile != definition.HookProfile
                || publication.BudgetProfile != definition.Components.BudgetProfile)
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.run-profile.key-mismatch",
                    $"Agent '{definition.Id}' selects profile keys that differ from its exact publication."));
            }

            ValidateComponents(definition, registrations, services, diagnostics);
            ValidateSecurityAuthority(definition, publication, services, diagnostics);
            ValidateSessionProfile(definition, publication.SessionProfile, registrations, services, diagnostics);
            ValidateModels(definition, services, ref modelSnapshot, ref modelSnapshotFailed, diagnostics);
            ValidateOptionalCapabilities(definition, registrations, services, diagnostics);
        }

        return profileSnapshot;
    }

    /// <summary>Requires one registration under each exact contract and key <see cref="AgentComponentSelection"/> names.</summary>
    private static void ValidateComponents(
        AgentDefinition definition,
        ComponentRegistrationSnapshot registrations,
        DefinitionValidationServices services,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(definition is not null, "Definition validation supplies a published definition.");
        var components = definition.Components;
        var id = definition.Id;

        // Loop ambiguity is reported engine-wide for every key, so only absence is a per-definition problem.
        RequireKeyed<IAgentLoop>(registrations, components.Loop.Value, id, "loop", reportAmbiguity: false, diagnostics);
        RequireKeyed<IRunContinuationPolicy>(registrations, components.ContinuationPolicy.Value, id, "continuation-policy", true, diagnostics);
        RequireKeyed<IInputCoordinator>(registrations, components.Input.Value, id, "input-coordinator", true, diagnostics);
        RequireKeyed<IOutputPublisher>(registrations, components.Output.Value, id, "output-publisher", true, diagnostics);
        RequireKeyed<IOutputProcessor>(registrations, components.OutputProcessor.Value, id, "output-processor", true, diagnostics);
        RequireKeyed<IContextAssembler>(registrations, components.Context.Value, id, "context-assembler", true, diagnostics);
        RequireKeyed<IModelSelector>(registrations, components.ModelSelector.Value, id, "model-selector", true, diagnostics);
        RequireKeyed<IModelRequestExecutor>(registrations, components.ModelExecutor.Value, id, "model-executor", true, diagnostics);

        // Collaborators the normative selection does not name keep a loop-key-scoped override with an unkeyed fallback.
        var loopKey = components.Loop.Value;
        RequireKeyedOrUnkeyed<ISessionCoordinator>(registrations, loopKey, id, diagnostics);
        RequireKeyedOrUnkeyed<ILlmModelResolver>(registrations, loopKey, id, diagnostics);
        RequireUnkeyed<ISessionRunCoordinator>(registrations, id, "run-coordinator", diagnostics);

        // The budget authority is an engine-wide singular proved by the registration check, so only the selected
        // profile is a per-definition fact.
        var budgetProfile = components.BudgetProfile;
        if (services.BudgetProfiles is not { } budgetProfiles || !budgetProfiles.TryGet(budgetProfile, out _))
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.definition.budget-profile.missing",
                $"Agent '{id}' selects budget profile '{budgetProfile.Value}' but it is not registered."));
        }
    }

    /// <summary>Requires the exact security publication's authority key to be an installed authority.</summary>
    /// <remarks>
    /// Skipped when the composition registers no <see cref="ISecurityAuthorityCatalog"/>: a replaced authority
    /// selector then owns its availability proof, and its runtime outcome stays authoritative.
    /// </remarks>
    private static void ValidateSecurityAuthority(
        AgentDefinition definition,
        AgentRunProfilePublication publication,
        DefinitionValidationServices services,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        if (services.SecurityAuthorities is { } authorities && !authorities.Contains(publication.SecurityProfile.AuthorityKey))
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.definition.security-authority.missing",
                $"Agent '{definition.Id}' selects security profile '{definition.SecurityProfile.Value}' whose authority '{publication.SecurityProfile.AuthorityKey.Value}' is not installed."));
        }
    }

    /// <summary>Requires the session profile's store to be a catalogued store that advertises what the profile needs.</summary>
    private static void ValidateSessionProfile(
        AgentDefinition definition,
        SessionProfileSnapshot profile,
        ComponentRegistrationSnapshot registrations,
        DefinitionValidationServices services,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        _ = registrations;
        if (services.SessionStores is not { } stores)
        {
            return;
        }

        var descriptors = stores.GetDescriptors().Where(descriptor => descriptor.Key == profile.DefaultStoreKey).ToArray();
        if (descriptors.Length == 0)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.definition.session-store.missing",
                $"Agent '{definition.Id}' selects session profile '{profile.Reference.Key.Value}' whose store '{profile.DefaultStoreKey.Value}' is not in the session store catalog."));
            return;
        }

        var descriptor = descriptors[0];
        if (descriptors.Length > 1
            || (profile.RequiredStoreCapabilities & descriptor.Capabilities) != profile.RequiredStoreCapabilities
            || (profile.RequiresDurableStore && !descriptor.Durable)
            || (profile.RequiresDistributedFencing && !descriptor.SupportsDistributedFencing))
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.definition.session-store.incompatible",
                $"Agent '{definition.Id}' selects session profile '{profile.Reference.Key.Value}' whose requirements store '{profile.DefaultStoreKey.Value}' does not advertise."));
        }
    }

    /// <summary>Requires at least one candidate that is a catalogued conversational model satisfying the stated requirements.</summary>
    private static void ValidateModels(
        AgentDefinition definition,
        DefinitionValidationServices services,
        ref ModelCatalogSnapshot? catalogSnapshot,
        ref bool catalogFailed,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        if (services.ModelCatalog is not { } modelCatalog)
        {
            return;
        }

        if (catalogSnapshot is null && !catalogFailed)
        {
            try
            {
                catalogSnapshot = modelCatalog.GetSnapshotAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                catalogFailed = true;
            }
        }

        if (catalogSnapshot is null)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.definition.model-catalog-unavailable",
                $"Agent '{definition.Id}' model candidates could not be checked because the model catalog snapshot is unavailable."));
            return;
        }

        var descriptors = definition.Models.Candidates
            .Select(catalogSnapshot.FindConversationModel)
            .Where(static descriptor => descriptor is not null)
            .Select(static descriptor => descriptor!)
            .ToArray();
        if (descriptors.Length == 0)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.definition.model.missing",
                $"Agent '{definition.Id}' selects no candidate that is a conversational model in the model catalog."));
            return;
        }

        var requirements = definition.Models.Requirements;
        if (requirements == ModelRequirements.None || services.CapabilityValidator is not { } validator)
        {
            return;
        }

        foreach (var descriptor in descriptors)
        {
            CapabilityValidationResult validation;
            try
            {
                validation = validator
                    .ValidateAsync(descriptor, requirements, definition.Models.Downgrade, CancellationToken.None)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.definition.model-catalog-unavailable",
                    $"Agent '{definition.Id}' model requirements could not be checked for alias '{descriptor.Alias.Value}'."));
                return;
            }

            if (validation is not CapabilitiesUnsupported)
            {
                return;
            }
        }

        diagnostics.Add(new CompositionDiagnostic(
            "agentkit.definition.model-incompatible",
            $"Agent '{definition.Id}' requires model capabilities that none of its configured candidates satisfy."));
    }

    /// <summary>Requires each enabled optional capability to resolve completely, and toolsets and their executor to agree.</summary>
    private static void ValidateOptionalCapabilities(
        AgentDefinition definition,
        ComponentRegistrationSnapshot registrations,
        DefinitionValidationServices services,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        var id = definition.Id;
        var capabilities = definition.OptionalCapabilities;

        if (!definition.Toolsets.IsEmpty && capabilities.ToolExecutor is null)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.definition.toolsets.executor-missing",
                $"Agent '{id}' selects toolsets but does not name a keyed {nameof(IToolExecutor)} in optional capabilities."));
        }

        if (definition.Toolsets.IsEmpty && capabilities.ToolExecutor is { } executorWithoutToolsets)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.definition.tool-executor.toolsets-missing",
                $"Agent '{id}' names tool executor '{executorWithoutToolsets.Value}' but selects no toolsets."));
        }

        if (capabilities.ToolExecutor is { } toolExecutor)
        {
            RequireKeyed<IToolExecutor>(registrations, toolExecutor.Value, id, "tool-executor", true, diagnostics);
        }
        else
        {
            RequireKeyedOrUnkeyed<IToolExecutor>(registrations, definition.Components.Loop.Value, id, diagnostics);
        }

        foreach (var toolset in definition.Toolsets)
        {
            var registered = registrations.Services.Any(descriptor =>
                descriptor.IsKeyedService
                && descriptor.ServiceType == typeof(ToolsetPublication)
                && descriptor.ServiceKey is ToolsetKey key
                && key == toolset.Key);
            if (!registered)
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.definition.toolset.missing",
                    $"Agent '{id}' selects toolset '{toolset.Key.Value}' but no publication is registered for it."));
            }
        }

        foreach (var reference in capabilities.Capabilities)
        {
            var sources = services.CapabilitySources.Where(source => source.CapabilityId == reference.CapabilityId).ToArray();
            if (sources.Length == 0)
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.definition.capability.unregistered",
                    $"Agent '{id}' references capability '{reference.CapabilityId.Value}' but no package registered it."));
            }
            else if (sources.Length > 1)
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.definition.capability.ambiguous",
                    $"Agent '{id}' references capability '{reference.CapabilityId.Value}' which {sources.Length} sources claim."));
            }
            else if (!sources[0].Contains(reference.ProfileId))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.definition.capability.profile-missing",
                    $"Agent '{id}' references capability '{reference.CapabilityId.Value}' profile '{reference.ProfileId.Value}' which is not registered."));
            }
        }
    }

    /// <summary>Requires exactly one keyed registration for one exact contract and key.</summary>
    private static void RequireKeyed<TService>(
        ComponentRegistrationSnapshot registrations,
        string key,
        AgentId agentId,
        string role,
        bool reportAmbiguity,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
        where TService : class
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(key), "Component keys are validated nonblank at construction.");
        var count = registrations.Services.Count(service =>
            service.IsKeyedService
            && service.ServiceType == typeof(TService)
            && key.Equals(service.ServiceKey as string, StringComparison.Ordinal));
        if (count == 0)
        {
            diagnostics.Add(new CompositionDiagnostic(
                $"agentkit.definition.{role}.missing",
                $"Agent '{agentId}' selects {typeof(TService).Name} key '{key}' but no registration exists for that exact key."));
        }
        else if (count > 1 && reportAmbiguity)
        {
            diagnostics.Add(new CompositionDiagnostic(
                $"agentkit.definition.{role}.ambiguous",
                $"Agent '{agentId}' selects {typeof(TService).Name} key '{key}' but {count} registrations claim it."));
        }
    }

    /// <summary>Requires a registration keyed to <paramref name="loopKey"/>, or an unkeyed fallback, for one collaborator contract.</summary>
    private static void RequireKeyedOrUnkeyed<TService>(
        ComponentRegistrationSnapshot registrations,
        string loopKey,
        AgentId agentId,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
        where TService : class
    {
        var hasRegistration = registrations.Services.Any(service =>
            service.ServiceType == typeof(TService)
            && (!service.IsKeyedService || loopKey.Equals(service.ServiceKey as string, StringComparison.Ordinal)));
        if (!hasRegistration)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.definition.collaborator.missing",
                $"Agent '{agentId}' selects loop key '{loopKey}' but no keyed or unkeyed {typeof(TService).Name} is registered."));
        }
    }

    /// <summary>Requires an unkeyed registration for one engine-wide collaborator contract.</summary>
    private static void RequireUnkeyed<TService>(
        ComponentRegistrationSnapshot registrations,
        AgentId agentId,
        string role,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
        where TService : class
    {
        if (!registrations.Services.Any(service => !service.IsKeyedService && service.ServiceType == typeof(TService)))
        {
            diagnostics.Add(new CompositionDiagnostic(
                $"agentkit.definition.{role}.missing",
                $"Agent '{agentId}' requires an unkeyed {typeof(TService).Name} registration."));
        }
    }
}
