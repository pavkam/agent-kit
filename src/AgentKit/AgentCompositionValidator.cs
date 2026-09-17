// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Checks that a built composition can actually run an agent.
/// </summary>
/// <remarks>
/// <para>
/// Microsoft DI's own build validation proves that registered constructors can
/// be satisfied. It cannot know that AgentKit additionally needs a definition
/// catalog with at least one published agent, and a loop resolvable from a run
/// scope. This validator adds those engine-level requirements.
/// </para>
/// <para>
/// Every problem is collected rather than thrown on first failure, so a
/// misconfigured composition reports its complete set of mistakes once.
/// </para>
/// </remarks>
internal static class AgentCompositionValidator
{
    /// <summary>
    /// Validates that <paramref name="provider"/> can run at least one agent.
    /// </summary>
    /// <param name="provider">The freshly built composition to inspect.</param>
    /// <exception cref="AgentCompositionException">
    /// The composition is missing a required engine-wide service, publishes no
    /// runnable agent definition, or cannot resolve a loop for a run.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <returns>
    /// The exact immutable run-profile and partial component-registration
    /// evidence inspected by validation. The caller must pass this same
    /// instance into engine construction so replaceable readers or later
    /// service-collection mutation cannot exchange evidence between stages.
    /// </returns>
    public static AgentCompositionSnapshot Validate(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var componentRegistrations = provider.GetService<ComponentRegistrationSnapshot>()
            ?? throw new AgentCompositionException([
                new CompositionDiagnostic(
                    "agentkit.component-registration.snapshot-missing",
                    "No build-local component registration snapshot is registered. Hosted compositions must use AgentKitServiceProviderFactory."),
            ]);

        return Validate(provider, componentRegistrations);
    }

    /// <summary>Validates a provider against the exact registration snapshot already captured for this build.</summary>
    /// <param name="provider">The freshly built non-null composition to inspect.</param>
    /// <param name="componentRegistrations">The non-null build-local registration evidence to validate and retain.</param>
    /// <returns>The exact immutable readiness evidence inspected by validation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> or <paramref name="componentRegistrations"/> is <see langword="null"/>.</exception>
    /// <exception cref="AgentCompositionException">Declared graph, DI correspondence, or reduced runnable readiness validation fails.</exception>
    internal static AgentCompositionSnapshot Validate(
        IServiceProvider provider,
        ComponentRegistrationSnapshot componentRegistrations)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(componentRegistrations);

        ValidateComponentRegistrations(componentRegistrations, requiresFacade: true);

        var diagnostics = ImmutableArray.CreateBuilder<CompositionDiagnostic>();

        var catalog = Resolve<IAgentDefinitionCatalog>(provider, diagnostics, "agentkit.catalog.missing");
        var profileReader = Resolve<IAgentRunProfilePublicationReader>(
            provider, diagnostics, "agentkit.run-profile-reader.missing");
        _ = Resolve<ISecurityProfileSelector>(provider, diagnostics, "agentkit.security-profile-selector.missing");
        _ = Resolve<TimeProvider>(provider, diagnostics, "agentkit.time.missing");
        _ = Resolve<IIdentifierGenerator<RunId>>(provider, diagnostics, "agentkit.runid.missing");
        _ = Resolve<IIdentifierGenerator<OperationId>>(provider, diagnostics, "agentkit.operationid.missing");
        // IModelCatalog is always resolved unkeyed by AgentRunServicesFactory.Compile (per-loop catalog
        // selection is not a supported axis), so one engine-wide check covers every definition.
        _ = Resolve<IModelCatalog>(provider, diagnostics, "agentkit.model-catalog.missing");
        // The continuation policy is resolved from one fixed, well-known key rather than per loop, so
        // this is a single engine-wide check rather than one per runnable definition.
        if (!componentRegistrations.Services.Any(service =>
            service.IsKeyedService
            && service.ServiceType == typeof(IRunContinuationPolicy)
            && AgentLoopComponentDefaults.ContinuationPolicyKey.Value.Equals(service.ServiceKey as string, StringComparison.Ordinal)))
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.continuation-policy.missing",
                $"No IRunContinuationPolicy is registered under the fixed key '{AgentLoopComponentDefaults.ContinuationPolicyKey.Value}'."));
        }

        AgentRunProfilePublicationSnapshot? validatedRunProfiles = null;
        if (catalog is not null)
        {
            validatedRunProfiles = ValidateCatalog(catalog, profileReader, componentRegistrations, diagnostics);
        }

        if (diagnostics.Count > 0)
        {
            throw new AgentCompositionException(diagnostics.ToImmutable());
        }

        Debug.Assert(validatedRunProfiles is not null,
            "A runnable composition must have one validated run-profile snapshot.");
        return new AgentCompositionSnapshot(validatedRunProfiles, componentRegistrations);
    }

    /// <summary>Validates the declared closed graph and its actual DI correspondence before application services are resolved.</summary>
    /// <param name="snapshot">The non-null build-local registration evidence.</param>
    /// <param name="requiresFacade">Whether the caller is constructing an engine even if its facade registration was removed. Otherwise feature-only hosts need no runnable spine.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
    /// <exception cref="AgentCompositionException">The declared graph, its Microsoft DI correspondence, or required facade service cardinality is invalid. An absent declaration set remains explicitly partial and is not treated as complete graph validation.</exception>
    internal static void ValidateComponentRegistrations(ComponentRegistrationSnapshot snapshot, bool requiresFacade = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ImmutableArray<CompositionDiagnostic> diagnostics =
        [
            .. ComponentDependencyGraphValidator.ValidateSnapshot(snapshot),
            .. ComponentRegistrationCorrespondenceValidator.Validate(snapshot),
            .. ValidateRequiredFacadeServices(snapshot, requiresFacade),
        ];
        if (!diagnostics.IsEmpty)
        {
            throw new AgentCompositionException(diagnostics);
        }
    }

    /// <summary>Validates required singular facade services from exact build-local DI descriptors.</summary>
    /// <param name="snapshot">The non-null build-local registration evidence.</param>
    /// <param name="requiresFacade">Whether engine construction requires the facade even when no unkeyed facade descriptor remains.</param>
    /// <returns>Bounded missing or ambiguous service diagnostics without resolving registrations.</returns>
    private static ImmutableArray<CompositionDiagnostic> ValidateRequiredFacadeServices(
        ComponentRegistrationSnapshot snapshot,
        bool requiresFacade)
    {
        Debug.Assert(snapshot is not null, "Component registration validation supplies a non-null snapshot.");

        var hasFacade = snapshot.Services.Any(static service =>
            !service.IsKeyedService && service.ServiceType == typeof(AgentEngine));
        if (!hasFacade && !requiresFacade)
        {
            return [];
        }

        var diagnostics = ImmutableArray.CreateBuilder<CompositionDiagnostic>();
        ValidateSingularRegistration<AgentEngine>(snapshot, diagnostics, "agentkit.engine");
        ValidateSingularRegistration<IAgentDefinitionCatalog>(snapshot, diagnostics, "agentkit.catalog");
        ValidateSingularRegistration<IAgentRunProfilePublicationReader>(snapshot, diagnostics, "agentkit.run-profile-reader");
        ValidateSingularRegistration<ISecurityProfileSelector>(snapshot, diagnostics, "agentkit.security-profile-selector");
        ValidateSingularRegistration<ISecurityGrantStore>(snapshot, diagnostics, "agentkit.security-grant-store");
        ValidateSingularRegistration<TimeProvider>(snapshot, diagnostics, "agentkit.time");
        ValidateSingularRegistration<IIdentifierGenerator<RunId>>(snapshot, diagnostics, "agentkit.runid");
        ValidateSingularRegistration<IIdentifierGenerator<OperationId>>(snapshot, diagnostics, "agentkit.operationid");
        ValidateAgentLoopRegistered(snapshot, diagnostics);
        return diagnostics.ToImmutable();
    }

    /// <summary>Checks that at least one keyed <see cref="IAgentLoop"/> registration exists, without resolving it.</summary>
    /// <param name="snapshot">The non-null immutable build-local registrations.</param>
    /// <param name="diagnostics">The initialized collector for deterministic missing diagnostics.</param>
    /// <remarks>
    /// <see cref="IAgentLoop"/> is deliberately keyed and scoped rather than singular and unkeyed (see
    /// <see cref="ValidateSingularRegistration{TService}"/>), so every agent definition can select its own loop
    /// and, through it, its own compiled <see cref="AgentRunServices"/> bundle. This check only proves that some
    /// keyed registration exists; <see cref="ValidateCatalog"/> proves that every published definition's exact
    /// selected key — or the engine-wide default when it selects none — actually resolves.
    /// </remarks>
    private static void ValidateAgentLoopRegistered(
        ComponentRegistrationSnapshot snapshot,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(snapshot is not null, "Composition validation supplies captured registrations.");
        Debug.Assert(diagnostics is not null, "Composition validation owns an initialized diagnostic collector.");

        var keyedLoopDescriptors = snapshot.Services
            .Where(static service => service.IsKeyedService && service.ServiceType == typeof(IAgentLoop))
            .ToArray();
        if (keyedLoopDescriptors.Length == 0)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.loop.unresolvable",
                "Expected at least one keyed IAgentLoop registration; found none. Call AddAgentLoop with an explicit key."));
            return;
        }

        foreach (var group in keyedLoopDescriptors.GroupBy(static service => service.ServiceKey as string ?? string.Empty))
        {
            var count = group.Count();
            if (count != 1)
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.loop.ambiguous",
                    $"Expected exactly one keyed IAgentLoop registration for key '{group.Key}'; found {count}. Use ReplaceAgentLoop to replace an existing registration explicitly."));
            }
        }
    }

    /// <summary>Checks one required unkeyed registration without activating application services.</summary>
    /// <typeparam name="TService">The singular service required by the current facade runtime.</typeparam>
    /// <param name="snapshot">The non-null immutable build-local registrations.</param>
    /// <param name="diagnostics">The initialized collector for deterministic missing and ambiguous diagnostics.</param>
    /// <param name="code">The nonempty stable diagnostic-code prefix for this service.</param>
    /// <param name="missingSuffix">The nonempty existing diagnostic suffix used when the service is missing.</param>
    /// <remarks>Keyed alternatives do not satisfy or conflict with an unkeyed requirement. No service keys, factories, constructors, or instance callbacks are invoked.</remarks>
    private static void ValidateSingularRegistration<TService>(
        ComponentRegistrationSnapshot snapshot,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics,
        string code,
        string missingSuffix = "missing")
    {
        Debug.Assert(snapshot is not null, "Composition validation supplies captured registrations.");
        Debug.Assert(diagnostics is not null, "Composition validation owns an initialized diagnostic collector.");
        Debug.Assert(!string.IsNullOrWhiteSpace(code), "Every required service has a stable diagnostic-code prefix.");
        Debug.Assert(!string.IsNullOrWhiteSpace(missingSuffix), "Missing service diagnostics have a nonempty suffix.");

        var count = snapshot.Services.Count(static descriptor =>
            !descriptor.IsKeyedService && descriptor.ServiceType == typeof(TService));
        if (count != 1)
        {
            diagnostics.Add(new CompositionDiagnostic(
                $"{code}.{(count == 0 ? missingSuffix : "ambiguous")}",
                $"Expected exactly one unkeyed {typeof(TService).Name} registration; found {count}. Select one implementation explicitly."));
        }
    }

    private static AgentRunProfilePublicationSnapshot? ValidateCatalog(
        IAgentDefinitionCatalog catalog,
        IAgentRunProfilePublicationReader? profileReader,
        ComponentRegistrationSnapshot componentRegistrations,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
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

        foreach (var definition in snapshot.Definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.SecurityProfile.Value)
                || string.IsNullOrWhiteSpace(definition.SessionProfile.Value))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.definition.profiles.missing",
                    $"Agent '{definition.Id}' does not explicitly select security and session profiles."));
                continue;
            }

            if (!publications.TryGetValue((definition.Id, definition.Revision), out var publication))
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.run-profile.missing",
                    $"Agent '{definition.Id}' has no exact run-profile publication for revision {definition.Revision}."));
                continue;
            }

            if (publication.SecurityProfile.ProfileKey != definition.SecurityProfile
                || publication.SessionProfile.Reference.Key != definition.SessionProfile)
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.run-profile.key-mismatch",
                    $"Agent '{definition.Id}' selects profile keys that differ from its exact publication."));
            }

            var loopKey = (definition.LoopKey ?? AgentLoopComponentDefaults.LoopKey).Value;
            var hasSelectedLoop = componentRegistrations.Services.Any(service =>
                service.IsKeyedService
                && service.ServiceType == typeof(IAgentLoop)
                && loopKey.Equals(service.ServiceKey as string, StringComparison.Ordinal));
            if (!hasSelectedLoop)
            {
                diagnostics.Add(new CompositionDiagnostic(
                    "agentkit.definition.loop.missing",
                    $"Agent '{definition.Id}' selects loop key '{loopKey}' but no keyed IAgentLoop is registered for it."));
            }

            // AgentRunServicesFactory.Compile resolves every one of these through
            // ResolveKeyedOrShared: a registration keyed to this exact loop key when one exists,
            // otherwise the engine-wide unkeyed registration. Composition must fail here, not on the
            // first RunAsync, when a definition's loop key has neither.
            RequireKeyedOrUnkeyed<ISessionCoordinator>(componentRegistrations, loopKey, definition.Id, diagnostics);
            RequireKeyedOrUnkeyed<IContextAssembler>(componentRegistrations, loopKey, definition.Id, diagnostics);
            RequireKeyedOrUnkeyed<IToolInvoker>(componentRegistrations, loopKey, definition.Id, diagnostics);
            RequireKeyedOrUnkeyed<IModelSelector>(componentRegistrations, loopKey, definition.Id, diagnostics);
            RequireKeyedOrUnkeyed<ILlmModelResolver>(componentRegistrations, loopKey, definition.Id, diagnostics);
        }

        return profileSnapshot;
    }

    /// <summary>Requires a registration keyed to <paramref name="loopKey"/>, or an unkeyed fallback, for one collaborator contract.</summary>
    /// <typeparam name="TService">The collaborator contract <see cref="AgentRunServicesFactory"/> resolves through <c>ResolveKeyedOrShared</c>.</typeparam>
    private static void RequireKeyedOrUnkeyed<TService>(
        ComponentRegistrationSnapshot componentRegistrations,
        string loopKey,
        AgentId agentId,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
        where TService : class
    {
        var hasRegistration = componentRegistrations.Services.Any(service =>
            service.ServiceType == typeof(TService)
            && (!service.IsKeyedService || loopKey.Equals(service.ServiceKey as string, StringComparison.Ordinal)));
        if (!hasRegistration)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.definition.collaborator.missing",
                $"Agent '{agentId}' selects loop key '{loopKey}' but no keyed or unkeyed {typeof(TService).Name} is registered."));
        }
    }

    private static TService? Resolve<TService>(
        IServiceProvider provider,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics,
        string code)
        where TService : class
    {
        var service = provider.GetService<TService>();
        if (service is null)
        {
            diagnostics.Add(new CompositionDiagnostic(
                code,
                $"No {typeof(TService).Name} is registered. Call AddAgentKit on the service collection."));
        }

        return service;
    }
}
