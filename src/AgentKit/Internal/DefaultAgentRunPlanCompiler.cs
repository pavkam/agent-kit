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
/// run's keyed loop has been resolved. For every collaborator a host might register under that same loop key, it
/// prefers the keyed registration and falls back to the engine-wide unkeyed registration. <see cref="IModelCatalog"/>
/// stays unkeyed because composition requires exactly one engine-wide catalog. The continuation policy is resolved
/// from <see cref="AgentLoopComponentDefaults.ContinuationPolicyKey"/>, not from the loop key.
/// </para>
/// <para>
/// The output processor, compactor, budget authority, run coordinator, input coordinator, and output publisher are
/// optional and resolved unkeyed. A definition that selects an output contract without a processor fails closed
/// inside the loop. Routing those optional collaborators through the definition's own keys is a known follow-up.
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

        var loopKey = definition.Definition.LoopKey ?? AgentLoopComponentDefaults.LoopKey;
        var loop = _provider.GetRequiredKeyedService<IAgentLoop>(loopKey.Value);
        var sessions = ResolveKeyedOrShared<ISessionCoordinator>(_provider, loopKey.Value);
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
            CompileServices(_provider, loopKey),
            capability,
            authorization,
            AgentOptionalCapabilitySelection.None));
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

    private static AgentRunServices CompileServices(IServiceProvider provider, ComponentKey<IAgentLoop> loopKey)
    {
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
