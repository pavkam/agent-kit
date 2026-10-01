// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using AgentKit.Internal;

/// <summary>
/// Checks that a built composition can actually run an agent.
/// </summary>
/// <remarks>
/// <para>
/// Microsoft DI's own build validation proves that registered constructors can
/// be satisfied. It cannot know that AgentKit additionally needs a definition
/// catalog with at least one published agent and every engine-wide singular the
/// architecture names: definition catalog, run-scope factory, session
/// directory/store catalog/selector, hook kernel/catalog/profile selector,
/// security authority selector/policy catalog, approval broker, model catalog,
/// provider-profile runtime selector, budget authority, <see cref="TimeProvider"/>,
/// <see cref="IRandomizerFactory"/>, and <see cref="IContentHasher"/>. This validator checks
/// those singular registrations from the frozen descriptors without invoking a
/// factory, then delegates every published definition's keyed selections,
/// profiles, and optional capabilities to <see cref="DefinitionCompositionValidator"/>.
/// </para>
/// <para>
/// The validator is a facade-internal static boundary rather than a registered
/// service: a replaceable validator would let a composition skip the proof it
/// exists to make, so the "one composition validator" requirement is satisfied
/// by construction.
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
    /// <exception cref="AgentCompositionException">Declared graph, DI correspondence, or runnable readiness validation fails.</exception>
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
        _ = Resolve<ISecurityAuthoritySelector>(provider, diagnostics, "agentkit.security-authority-selector.missing");
        _ = Resolve<ISecurityPolicyCatalog>(provider, diagnostics, "agentkit.security-policy-catalog.missing");
        _ = Resolve<IApprovalBroker>(provider, diagnostics, "agentkit.approval-broker.missing");
        _ = Resolve<ISecurityAuditDispatcher>(provider, diagnostics, "agentkit.security-audit-dispatcher.missing");
        _ = Resolve<IApprovalStore>(provider, diagnostics, "agentkit.approval-store.missing");
        _ = Resolve<ISecurityDecisionStore>(provider, diagnostics, "agentkit.security-decision-store.missing");
        _ = Resolve<TimeProvider>(provider, diagnostics, "agentkit.time.missing");
        _ = Resolve<IIdentifierGenerator<RunId>>(provider, diagnostics, "agentkit.runid.missing");
        _ = Resolve<IIdentifierGenerator<OperationId>>(provider, diagnostics, "agentkit.operationid.missing");
        // IModelCatalog is always resolved unkeyed by DefaultAgentRunPlanCompiler.CompileServices (per-loop catalog
        // selection is not a supported axis), so one engine-wide check covers every definition.
        _ = Resolve<IModelCatalog>(provider, diagnostics, "agentkit.model-catalog.missing");
        _ = Resolve<IBudgetAuthority>(provider, diagnostics, "agentkit.budget-authority.missing");
        _ = Resolve<IRandomizerFactory>(provider, diagnostics, "agentkit.randomizer-factory.missing");
        _ = Resolve<IContentHasher>(provider, diagnostics, "agentkit.content-hasher.missing");
        HookCompositionValidator.Validate(provider, diagnostics);
        AgentRunProfilePublicationSnapshot? validatedRunProfiles = null;
        if (catalog is not null)
        {
            var services = new DefinitionValidationServices(
                provider.GetService<IModelCatalog>(),
                provider.GetService<IModelCapabilityValidator>(),
                provider.GetService<IBudgetProfileCatalog>(),
                provider.GetService<ISessionStoreCatalog>(),
                provider.GetService<ISecurityAuthorityCatalog>(),
                [.. provider.GetServices<IAgentCapabilityProfileSource>()]);
            validatedRunProfiles = DefinitionCompositionValidator.Validate(
                catalog,
                profileReader,
                componentRegistrations,
                services,
                diagnostics);
            var hookProfileSelector = provider.GetService<IHookProfileSelector>();
            if (hookProfileSelector is not null)
            {
                HookCompositionValidator.ValidateDefinitionHookProfiles(catalog, hookProfileSelector, diagnostics);
            }

            if (catalog.CurrentSnapshot is { } durabilitySnapshot)
            {
                DurabilityCompositionValidator.Validate(
                    durabilitySnapshot.Definitions,
                    provider.GetService<IDurabilityProfileCatalog>(),
                    componentRegistrations,
                    durabilitySnapshot.Definitions.Any(static definition => definition.OptionalCapabilities.DurabilityProfile is not null)
                        ? [.. provider.GetServices<IDurableOperationHandler>()]
                        : [],
                    diagnostics);
                CompactionCompositionValidator.Validate(
                    durabilitySnapshot.Definitions,
                    provider,
                    componentRegistrations,
                    diagnostics);
                GoalsCompositionValidator.Validate(
                    durabilitySnapshot.Definitions,
                    provider.GetService<IGoalProfileCatalog>(),
                    componentRegistrations,
                    diagnostics);
                ValidateMemory(durabilitySnapshot.Definitions, provider, componentRegistrations, diagnostics);
                ValidateArtifacts(durabilitySnapshot.Definitions, provider, componentRegistrations, diagnostics);
            }
        }

        if (diagnostics.Count > 0)
        {
            throw new AgentCompositionException(diagnostics.ToImmutable());
        }

        Debug.Assert(validatedRunProfiles is not null,
            "A runnable composition must have one validated run-profile snapshot.");
        return new AgentCompositionSnapshot(validatedRunProfiles, componentRegistrations);
    }

    private static void ValidateArtifacts(
        ImmutableArray<AgentDefinition> definitions,
        IServiceProvider provider,
        ComponentRegistrationSnapshot componentRegistrations,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        IArtifactCoordinatorCatalog? catalog = null;
        string? catalogFailure = null;
        if (definitions.Any(static definition => definition.OptionalCapabilities.ArtifactCoordinator is not null))
        {
            try
            {
                catalog = provider.GetService<IArtifactCoordinatorCatalog>();
            }
            catch (InvalidOperationException exception)
            {
                catalogFailure = exception.Message;
            }
        }

        ArtifactCompositionValidator.Validate(definitions, catalog, catalogFailure, componentRegistrations, diagnostics);
    }

    private static void ValidateMemory(
        ImmutableArray<AgentDefinition> definitions,
        IServiceProvider provider,
        ComponentRegistrationSnapshot componentRegistrations,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        IMemoryProfileCatalog? memoryCatalog = null;
        string? catalogFailure = null;
        if (definitions.Any(static definition => definition.OptionalCapabilities.MemoryProfile is not null))
        {
            try
            {
                memoryCatalog = provider.GetService<IMemoryProfileCatalog>();
            }
            catch (InvalidOperationException exception)
            {
                catalogFailure = exception.Message;
            }
        }

        MemoryCompositionValidator.Validate(definitions, memoryCatalog, catalogFailure, componentRegistrations, diagnostics);
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
        ValidateSingularRegistration<ISecurityAuthoritySelector>(snapshot, diagnostics, "agentkit.security-authority-selector");
        ValidateSingularRegistration<ISecurityPolicyCatalog>(snapshot, diagnostics, "agentkit.security-policy-catalog");
        ValidateSingularRegistration<IApprovalBroker>(snapshot, diagnostics, "agentkit.approval-broker");
        ValidateSingularRegistration<ISecurityAuditDispatcher>(snapshot, diagnostics, "agentkit.security-audit-dispatcher");
        ValidateSingularRegistration<IApprovalStore>(snapshot, diagnostics, "agentkit.approval-store");
        ValidateSingularRegistration<ISecurityDecisionStore>(snapshot, diagnostics, "agentkit.security-decision-store");
        ValidateSingularRegistration<ISecurityGrantStore>(snapshot, diagnostics, "agentkit.security-grant-store");
        ValidateSingularRegistration<TimeProvider>(snapshot, diagnostics, "agentkit.time");
        ValidateSingularRegistration<IIdentifierGenerator<RunId>>(snapshot, diagnostics, "agentkit.runid");
        ValidateSingularRegistration<IIdentifierGenerator<OperationId>>(snapshot, diagnostics, "agentkit.operationid");
        ValidateSingularRegistration<IProviderProfileRuntimeSelector>(snapshot, diagnostics, "agentkit.provider-profile-selector");
        ValidateSingularRegistration<IModelCatalog>(snapshot, diagnostics, "agentkit.model-catalog");
        ValidateSingularRegistration<IAgentRunScopeFactory>(snapshot, diagnostics, "agentkit.run-scope-factory");
        ValidateSingularRegistration<ISessionDirectory>(snapshot, diagnostics, "agentkit.session-directory");
        ValidateSingularRegistration<ISessionStoreCatalog>(snapshot, diagnostics, "agentkit.session-store-catalog");
        ValidateSingularRegistration<ISessionStoreSelector>(snapshot, diagnostics, "agentkit.session-store-selector");
        ValidateSingularRegistration<IBudgetAuthority>(snapshot, diagnostics, "agentkit.budget-authority");
        ValidateSingularRegistration<IBudgetProfileCatalog>(snapshot, diagnostics, "agentkit.budget-profile-catalog");
        ValidateSingularRegistration<IRandomizerFactory>(snapshot, diagnostics, "agentkit.randomizer-factory");
        ValidateSingularRegistration<IContentHasher>(snapshot, diagnostics, "agentkit.content-hasher");
        HookCompositionValidator.ValidateRegistrations(snapshot, diagnostics);
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
    /// keyed registration exists; <see cref="DefinitionCompositionValidator"/> proves that every published
    /// definition's exact selected key actually resolves.
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
