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

        ValidateComponentRegistrations(componentRegistrations);

        var diagnostics = ImmutableArray.CreateBuilder<CompositionDiagnostic>();

        var catalog = Resolve<IAgentDefinitionCatalog>(provider, diagnostics, "agentkit.catalog.missing");
        var profileReader = Resolve<IAgentRunProfilePublicationReader>(
            provider, diagnostics, "agentkit.run-profile-reader.missing");
        _ = Resolve<ISecurityProfileSelector>(provider, diagnostics, "agentkit.security-profile-selector.missing");
        _ = Resolve<TimeProvider>(provider, diagnostics, "agentkit.time.missing");
        _ = Resolve<IIdentifierGenerator<RunId>>(provider, diagnostics, "agentkit.runid.missing");
        _ = Resolve<IIdentifierGenerator<OperationId>>(provider, diagnostics, "agentkit.operationid.missing");

        AgentRunProfilePublicationSnapshot? validatedRunProfiles = null;
        if (catalog is not null)
        {
            validatedRunProfiles = ValidateCatalog(catalog, profileReader, diagnostics);
        }

        ValidateRunScopeRegistration(componentRegistrations, diagnostics);

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
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
    /// <exception cref="AgentCompositionException">The declared graph or its Microsoft DI correspondence is invalid. An absent declaration set remains explicitly partial and is not treated as complete graph validation.</exception>
    internal static void ValidateComponentRegistrations(ComponentRegistrationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ImmutableArray<CompositionDiagnostic> diagnostics =
        [
            .. ComponentDependencyGraphValidator.ValidateSnapshot(snapshot),
            .. ComponentRegistrationCorrespondenceValidator.Validate(snapshot),
            .. ValidateRequiredFacadeServices(snapshot),
        ];
        if (!diagnostics.IsEmpty)
        {
            throw new AgentCompositionException(diagnostics);
        }
    }

    /// <summary>Validates required singular facade services from exact build-local DI descriptors.</summary>
    /// <param name="snapshot">The non-null build-local registration evidence.</param>
    /// <returns>Bounded missing or ambiguous service diagnostics without resolving registrations.</returns>
    private static ImmutableArray<CompositionDiagnostic> ValidateRequiredFacadeServices(
        ComponentRegistrationSnapshot snapshot)
    {
        Debug.Assert(snapshot is not null, "Component registration validation supplies a non-null snapshot.");

        var hasFacade = snapshot.Services.Any(static service =>
            !service.IsKeyedService && service.ServiceType == typeof(AgentEngine));
        if (!hasFacade)
        {
            return [];
        }

        var grantStoreCount = snapshot.Services.Count(static service =>
            !service.IsKeyedService && service.ServiceType == typeof(ISecurityGrantStore));
        return grantStoreCount switch
        {
            0 =>
            [
                new CompositionDiagnostic(
                    "agentkit.security-grant-store.missing",
                    "No unkeyed ISecurityGrantStore is registered. Select one security grant-store adapter explicitly."),
            ],
            1 => [],
            _ =>
            [
                new CompositionDiagnostic(
                    "agentkit.security-grant-store.ambiguous",
                    "More than one unkeyed ISecurityGrantStore is registered. Register exactly one security grant-store adapter."),
            ],
        };
    }

    private static AgentRunProfilePublicationSnapshot? ValidateCatalog(
        IAgentDefinitionCatalog catalog,
        IAgentRunProfilePublicationReader? profileReader,
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
        }

        return profileSnapshot;
    }

    /// <summary>Validates reduced loop readiness from frozen registration metadata without activating application services.</summary>
    /// <param name="snapshot">The exact build-local service descriptors already captured for validation.</param>
    /// <param name="diagnostics">The initialized collector that receives missing or ambiguous loop diagnostics.</param>
    /// <remarks>
    /// Constructor-graph validation remains Microsoft DI's responsibility. This check proves only that the current
    /// reduced runtime has exactly one unkeyed loop registration; selected keyed loop validation belongs to the
    /// canonical agent-component selection checkpoint.
    /// </remarks>
    private static void ValidateRunScopeRegistration(
        ComponentRegistrationSnapshot snapshot,
        ImmutableArray<CompositionDiagnostic>.Builder diagnostics)
    {
        Debug.Assert(snapshot is not null, "Composition validation supplies a non-null registration snapshot.");
        Debug.Assert(diagnostics is not null, "Composition validation owns an initialized diagnostic collector.");

        var registrations = snapshot.Services.Count(static descriptor =>
            !descriptor.IsKeyedService && descriptor.ServiceType == typeof(IAgentLoop));
        if (registrations == 0)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.loop.unresolvable",
                $"No unkeyed {nameof(IAgentLoop)} is registered for the current reduced runtime. Register one with AddAgentLoop."));
        }
        else if (registrations > 1)
        {
            diagnostics.Add(new CompositionDiagnostic(
                "agentkit.loop.ambiguous",
                $"More than one unkeyed {nameof(IAgentLoop)} is registered for the current reduced runtime. Register exactly one."));
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
