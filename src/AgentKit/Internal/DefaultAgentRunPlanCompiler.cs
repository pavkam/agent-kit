// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Internal;

using AgentKit;

/// <summary>
/// Compiles the immutable <see cref="AgentRunPlan"/> a run's selected <see cref="IAgentLoop"/> drives with.
/// </summary>
/// <remarks>
/// <para>
/// This is the run-activation boundary. It runs once per run, inside the freshly created run scope, after the
/// run's keyed loop has been resolved. <see cref="IModelCatalog"/> stays unkeyed because composition requires exactly
/// one engine-wide catalog.
/// </para>
/// <para>
/// The input coordinator, output processor, context assembler, model selector, model request
/// executor, and continuation policy resolve under the exact keys the definition's
/// <see cref="AgentDefinition.Components"/> select. The compactor and run coordinator remain optional and unkeyed;
/// the budget authority and profile catalog are engine-wide singulars the definition's budget profile resolves through.
/// </para>
/// <para>
/// <see cref="SessionExecutionCapability"/> is installed into <see cref="RunScopeState"/> before the rest of the
/// bundle is resolved, so a scoped collaborator that needs the capability observes this run's instance.
/// </para>
/// </remarks>
internal sealed class DefaultAgentRunPlanCompiler: IAgentRunPlanCompiler
{
    private readonly IServiceProvider _provider;
    private readonly IAgentDefinitionCatalog _catalog;
    private readonly IAgentRunProfilePublicationReader _publications;
    private readonly ISecurityProfileSelector _securityProfiles;
    private readonly IIdentifierGenerator<OperationId> _operationIds;

    /// <summary>Initializes the compiler over one run scope and the engine-wide readers it revalidates.</summary>
    /// <param name="provider">The run scope's service provider.</param>
    /// <param name="catalog">The catalog whose current snapshot the pinned definition is checked against.</param>
    /// <param name="publications">The reader that supplies the run-profile publication.</param>
    /// <param name="securityProfiles">The selector that captures fresh authorization.</param>
    /// <param name="operationIds">The generator for the before-run operation correlation.</param>
    /// <exception cref="ArgumentNullException">A required collaborator is null.</exception>
    public DefaultAgentRunPlanCompiler(
        IServiceProvider provider,
        IAgentDefinitionCatalog catalog,
        IAgentRunProfilePublicationReader publications,
        ISecurityProfileSelector securityProfiles,
        IIdentifierGenerator<OperationId> operationIds)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(publications);
        ArgumentNullException.ThrowIfNull(securityProfiles);
        ArgumentNullException.ThrowIfNull(operationIds);
        _provider = provider;
        _catalog = catalog;
        _publications = publications;
        _securityProfiles = securityProfiles;
        _operationIds = operationIds;
    }

    /// <inheritdoc/>
    public async ValueTask<AgentRunPlanCompilationResult> CompileAsync(
        ResolvedAgentDefinition definition,
        AgentRunRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = await _catalog.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var current = snapshot.FindDefinition(definition.Definition.Id);
        if (current is null || current.Revision != definition.Definition.Revision || !current.Equals(definition.Definition))
        {
            return Invalid(
                "agentkit.run-plan.definition",
                current is null
                    ? "The pinned agent definition is no longer enabled for new admission."
                    : "The pinned agent definition was replaced and cannot be silently upgraded.");
        }

        var publicationResult = await _publications
            .ReadAsync(definition.Definition.Id, definition.Definition.Revision, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (publicationResult is not AgentRunProfilePublicationFound found)
        {
            return Invalid(
                "agentkit.run-plan.publication",
                "The built composition has no pinned run-profile publication for this definition.");
        }

        var components = definition.Definition.Components;
        var loop = _provider.GetRequiredKeyedService<IAgentLoop>(components.Loop.Value);
        var sessions = ResolveKeyedOrShared<ISessionCoordinator>(_provider, components.Loop.Value);
        var runCoordinator = _provider.GetRequiredService<ISessionRunCoordinator>();
        var capability = new SessionExecutionCapability(found.Publication.SessionProfile, sessions, runCoordinator);
        _provider.GetRequiredService<RunScopeState>().Session = capability;

        var authorization = await CaptureAsync(
            _securityProfiles,
            _operationIds,
            definition.Definition,
            snapshot.Version,
            found.Publication.SecurityProfile,
            request.SessionId,
            request.Identity,
            cancellationToken).ConfigureAwait(false);

        return new CompiledAgentRunPlan(new AgentRunPlan(
            definition.Definition,
            snapshot.Version,
            loop,
            CompileServices(_provider, definition.Definition),
            capability,
            authorization,
            definition.Definition.OptionalCapabilities));
    }

    /// <summary>Resolves a collaborator keyed to the run's exact loop selection, falling back to the unkeyed registration.</summary>
    /// <typeparam name="TService">The collaborator contract.</typeparam>
    /// <param name="provider">The run's scoped service provider.</param>
    /// <param name="key">The exact loop key this run selected.</param>
    /// <returns>The keyed registration when one exists; otherwise the unkeyed registration.</returns>
    /// <exception cref="InvalidOperationException">Neither a keyed nor an unkeyed registration exists.</exception>
    internal static TService ResolveKeyedOrShared<TService>(IServiceProvider provider, string key)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return provider.GetKeyedService<TService>(key) ?? provider.GetRequiredService<TService>();
    }

    /// <summary>Captures fresh authorization and verifies it matches the pinned publication.</summary>
    /// <param name="selector">The security-profile selector.</param>
    /// <param name="operationIds">The operation-identity generator.</param>
    /// <param name="definition">The pinned definition.</param>
    /// <param name="catalogVersion">The catalog version to cite if capture fails.</param>
    /// <param name="security">The pinned security publication.</param>
    /// <param name="sessionId">The session the operation is scoped to, or null before a session exists.</param>
    /// <param name="identity">The already-authenticated caller identity.</param>
    /// <param name="cancellationToken">Cancels the selector call.</param>
    /// <returns>The captured authorization.</returns>
    /// <exception cref="ArgumentNullException">A required collaborator is null.</exception>
    /// <exception cref="AgentAdmissionRejectedException">Capture did not match the pinned publication.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    internal static async Task<SecurityAuthorizationContext> CaptureAsync(
        ISecurityProfileSelector selector,
        IIdentifierGenerator<OperationId> operationIds,
        AgentDefinition definition,
        AgentCatalogVersion catalogVersion,
        SecurityProfilePublication security,
        SessionId? sessionId,
        ExecutionIdentity identity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(operationIds);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(identity);

        var correlation = new BeforeRunOperationCorrelation(operationIds.Create(), null);
        return await CaptureAsync(
            selector, definition, catalogVersion, security, sessionId, correlation, identity, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Captures authorization for an already chosen operation correlation.</summary>
    /// <param name="selector">The security-profile selector.</param>
    /// <param name="definition">The pinned definition.</param>
    /// <param name="catalogVersion">The catalog version to cite if capture fails.</param>
    /// <param name="security">The pinned security publication.</param>
    /// <param name="sessionId">The session the operation is scoped to.</param>
    /// <param name="correlation">The operation correlation to bind into the authorization scope.</param>
    /// <param name="identity">The already-authenticated caller identity.</param>
    /// <param name="cancellationToken">Cancels the selector call.</param>
    /// <returns>The captured authorization when it matches <paramref name="security"/>.</returns>
    /// <exception cref="ArgumentNullException">A required collaborator is null.</exception>
    /// <exception cref="AgentAdmissionRejectedException">Capture did not match the pinned publication.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    internal static async Task<SecurityAuthorizationContext> CaptureAsync(
        ISecurityProfileSelector selector,
        AgentDefinition definition,
        AgentCatalogVersion catalogVersion,
        SecurityProfilePublication security,
        SessionId? sessionId,
        OperationCorrelation correlation,
        ExecutionIdentity identity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(identity);

        var result = await selector.SelectAsync(
            new SecurityAuthorizationCaptureRequest(
                new SecurityAuthorizationScope(definition.Id, sessionId, correlation),
                security.ProfileKey,
                security.AgentDefinitionRevision,
                security.ConfigurationVersion,
                identity),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result is SecurityAuthorizationCaptured captured
            && Matches(captured.Authorization, security, correlation, sessionId, identity)
            ? captured.Authorization
            : throw new AgentAdmissionRejectedException(new AgentAdmissionRejection(
                definition.Id,
                definition.Revision,
                catalogVersion,
                "Fresh authorization did not match the pinned security publication."));
    }

    /// <summary>Materializes every keyed collaborator the definition selects into one immutable bundle.</summary>
    /// <param name="provider">The run's scoped service provider.</param>
    /// <param name="definition">The pinned definition whose <see cref="AgentDefinition.Components"/> name each collaborator.</param>
    /// <returns>The bundle the selected loop drives with.</returns>
    /// <remarks>
    /// Every collaborator <see cref="AgentComponentSelection"/> names resolves exactly under its selected key; there is
    /// no unkeyed fallback, so a key the composition never registered fails here (and, earlier, in composition
    /// validation) rather than silently binding another definition's collaborator. Collaborators the normative
    /// selection does not name (session coordinator, security selector, model resolver, tool executor) keep the
    /// loop-key-scoped override with an engine-wide unkeyed fallback. The output publisher is the one selected
    /// component left out of the compiled bundle: it is scoped to the run's <see cref="RunScopeIdentity"/>, which the
    /// engine binds only after it mints the <see cref="RunId"/>, so <c>AgentEngineRuntime</c> resolves it under
    /// <see cref="AgentComponentSelection.Output"/> immediately after that binding.
    /// </remarks>
    private static AgentRunServices CompileServices(IServiceProvider provider, AgentDefinition definition)
    {
        var components = definition.Components;
        var sharedKey = components.Loop.Value;
        var toolExecutorKey = definition.OptionalCapabilities.ToolExecutor?.Value ?? sharedKey;
        var compaction = ResolveCompaction(provider, definition);
        return new AgentRunServices(
            ResolveKeyedOrShared<ISessionCoordinator>(provider, sharedKey),
            ResolveKeyedOrShared<ISecurityProfileSelector>(provider, sharedKey),
            provider.GetRequiredKeyedService<IContextAssembler>(components.Context.Value),
            ResolveKeyedOrShared<IToolExecutor>(provider, toolExecutorKey),
            provider.GetService<IToolRunCatalogCaptureFactory>(),
            provider.GetRequiredService<IModelCatalog>(),
            provider.GetRequiredKeyedService<IModelSelector>(components.ModelSelector.Value),
            ResolveKeyedOrShared<ILlmModelResolver>(provider, sharedKey),
            provider.GetRequiredKeyedService<IRunContinuationPolicy>(components.ContinuationPolicy.Value),
            provider.GetRequiredKeyedService<IOutputProcessor>(components.OutputProcessor.Value),
            compaction.Compactor,
            provider.GetRequiredService<IBudgetAuthority>(),
            provider.GetRequiredService<IBudgetProfileCatalog>(),
            provider.GetService<ISessionRunCoordinator>(),
            provider.GetRequiredKeyedService<IInputCoordinator>(components.Input.Value),
            publisher: null,
            provider.GetRequiredKeyedService<IModelRequestExecutor>(components.ModelExecutor.Value),
            compaction.Policy);
    }

    /// <summary>Resolves the compactor and compiled policy a definition's compaction selection names.</summary>
    /// <param name="provider">The run's scoped service provider.</param>
    /// <param name="definition">The pinned definition whose <see cref="AgentOptionalCapabilitySelection.CompactionProfile"/> is read.</param>
    /// <returns>The resolved selection; see <see cref="CompactionSelection"/> for each shape.</returns>
    /// <exception cref="InvalidOperationException">
    /// The definition selects a profile that the catalog does not publish, or an enabled profile whose compactor key has
    /// no registration; composition validation normally rejects both earlier.
    /// </exception>
    /// <remarks>
    /// A definition that selects no profile keeps the engine-wide unkeyed compactor, if any, and carries no policy, so
    /// the compactor applies its own default strategy order. A definition that selects a profile resolves the compactor
    /// under exactly the key the profile publishes; there is no unkeyed fallback, so one agent's profile can never bind
    /// another compactor.
    /// </remarks>
    internal static CompactionSelection ResolveCompaction(IServiceProvider provider, AgentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.OptionalCapabilities.CompactionProfile is not { } profile)
        {
            return new CompactionSelection(provider.GetService<ICompactor>(), null, Disabled: false);
        }

        var publication = provider.GetRequiredService<ICompactionProfileCatalog>().TryGet(profile, out var published)
            ? published
            : throw new InvalidOperationException(
                $"Compaction profile '{profile.Value}' selected by agent '{definition.Id}' is not published.");
        return publication.Enabled
            ? new CompactionSelection(
                provider.GetRequiredKeyedService<ICompactor>(publication.CompactorKey.Value),
                publication.Policy,
                Disabled: false)
            : new CompactionSelection(null, null, Disabled: true);
    }

    private static InvalidAgentRunPlan Invalid(string code, string safeMessage) =>
        new([new CompositionDiagnostic(code, safeMessage)]);

    private static bool Matches(
        SecurityAuthorizationContext authorization,
        SecurityProfilePublication publication,
        OperationCorrelation correlation,
        SessionId? sessionId,
        ExecutionIdentity identity) =>
        authorization.Scope.AgentId == publication.AgentId
        && authorization.Scope.SessionId == sessionId
        && authorization.Scope.Correlation == correlation
        && authorization.Identity == identity
        && authorization.ProfileKey == publication.ProfileKey
        && authorization.ProfileVersion == publication.ProfileVersion
        && authorization.PolicySnapshot == publication.PolicySnapshot
        && authorization.AuthorityKey == publication.AuthorityKey
        && authorization.AgentDefinitionRevision == publication.AgentDefinitionRevision
        && authorization.ConfigurationVersion == publication.ConfigurationVersion;
}
