// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using AgentKit.Permissions;
using AgentKit.Permissions.InMemory;
using AgentKit.TestSupport;

/// <summary>
/// Deterministic builders and fakes for engine composition tests.
/// </summary>
internal static class CompositionTestData
{
    extension(AgentResolution resolution)
    {
        /// <summary>Unwraps a resolution expected to be <see cref="ResolvedAgent"/>, failing the test otherwise.</summary>
        public Agent RequireResolved() => resolution.ShouldBeOfType<ResolvedAgent>().Agent;
    }


    public static AgentId AgentId { get; } =
        new(Guid.Parse("a0000000-0000-0000-0000-000000000001"));

    public static SessionId SessionId { get; } =
        new(Guid.Parse("b0000000-0000-0000-0000-000000000002"));

    public static BranchId BranchId { get; } =
        new(Guid.Parse("c0000000-0000-0000-0000-000000000003"));

    public static ExecutionIdentity Identity() =>
        TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    public static AgentDefinition Definition(
        AgentId? id = null,
        string displayName = "test agent",
        int maxTurns = 8,
        long revision = 1) =>
        new(
            id ?? AgentId,
            new AgentDefinitionRevision(revision),
            displayName,
            new ModelSelectionPolicy([new ModelAlias("chat")]),
            ModelRequirements.None,
            instructions: [],
            tools: [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            new RunPolicyDefaults(maxTurns, TimeSpan.FromMinutes(1)),
            ExtensionData.Empty,
            new SecurityProfileKey("security"),
            new SessionProfileKey("session"));

    public static AgentRunProfilePublication RunProfile(AgentDefinition definition) =>
        RunProfile(definition, SessionBusyBehavior.Reject);

    public static AgentRunProfilePublication RunProfile(AgentDefinition definition, SessionBusyBehavior busyBehavior) => new(
        new SecurityProfilePublication(
            definition.Id,
            definition.Revision,
            new ConfigurationVersion(1),
            definition.SecurityProfile,
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.Parse("d0000000-0000-0000-0000-000000000004")),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:test-policy")),
            new ComponentKey<ISecurityAuthority>("authority")),
        new SessionProfileSnapshot(
            new SessionProfileReference(definition.SessionProfile, new SessionProfileVersion(1)),
            new ComponentKey<ISessionCoordinator>("coordinator"),
            new ComponentKey<ISessionRunCoordinator>("run-coordinator"),
            new SessionStoreKey("store"),
            SessionStoreCapabilities.None,
            requiresDurableStore: false,
            requiresDistributedFencing: false,
            new SessionRetentionProfileKey("retention"),
            busyBehavior,
            maximumAppendEntries: 128,
            maximumPageSize: 256,
            verifySnapshotHashes: true,
            deleteOnDispose: false,
            new ContentHash("sha256:test-session-profile")),
        new EffectiveConfigurationSnapshot(
            new ConfigurationVersion(1),
            new ContentHash("sha256:test-session-profile"),
            [],
            []));

    public static AgentRunOptions RunOptions(int? maxTurns = null, TimeSpan? attemptTimeout = null) =>
        new(maxTurns, attemptTimeout);

    /// <summary>Builds the input the facade run tests admit.</summary>
    /// <returns>A steer input with one plain text part.</returns>
    public static AgentInput Input() =>
        new(
            new InputId(Guid.Parse("e0000000-0000-0000-0000-000000000005")),
            InputDelivery.Steer,
            [new TextPart("run", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

    /// <summary>Seeds one active session the runtime can open without creating it.</summary>
    /// <param name="sessions">The coordinator that will load the session.</param>
    /// <param name="agentId">The agent that owns the session.</param>
    /// <param name="sessionId">The session identity admission will open.</param>
    public static void SeedSession(InMemoryTestSessionCoordinator sessions, AgentId agentId, SessionId sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        var identity = Identity();
        sessions.Seed(new SessionDescriptor(
            new SessionAddress(agentId, sessionId),
            conversationId: null,
            identity.TenantId,
            identity.PrincipalId,
            new SessionStoreKey("store"),
            BranchId,
            new SessionVersion(0),
            SessionLifecycleState.Active,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            ExtensionData.Empty));
    }

    /// <summary>
    /// Reads the session version admission has already advanced, so a scripted loop can release the lane.
    /// </summary>
    /// <param name="request">The run request whose session is read.</param>
    /// <param name="services">The compiled collaborators, including the session coordinator.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The snapshot version, or <see langword="null"/> when the page has no snapshot.</returns>
    public static async Task<SessionVersion?> CurrentSessionVersionAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);
        var context = new SessionOperationContext(
            request.AgentId, request.SessionId, executionLaneId: null, request.Authorization.Scope.Correlation,
            request.Identity, request.Authorization);
        var page = await services.Session.ReadAsync(
            new SessionReadRequest(context, request.BranchId, new SessionSequence(0), pageSize: 1),
            request.SessionProfile, cancellationToken).ConfigureAwait(false);
        return page is SessionPage { Snapshot: { } snapshot } ? snapshot.Version : null;
    }

    /// <summary>
    /// Registers unbehavioral placeholder collaborators for every part of the compiled
    /// <see cref="AgentRunServices"/> bundle a scripted <see cref="IAgentLoop"/> like <see cref="RecordingAgentLoop"/>
    /// never actually reaches, so run-plan compilation can build the bundle without throwing.
    /// </summary>
    public static void AddRunServicesFakes(IServiceCollection services)
    {
        services.TryAddSingleton<ISessionCoordinator, UnsupportedSessionCoordinator>();
        services.TryAddSingleton<ISessionRunCoordinator, UnsupportedSessionRunCoordinator>();
        services.TryAddSingleton<IContextAssembler, UnsupportedContextAssembler>();
        services.TryAddSingleton<IToolExecutor, CaptureTestToolExecutor>();
        services.TryAddSingleton<IModelCatalog>(
            new StaticModelCatalog(new ModelCatalogSnapshot(new ModelCatalogVersion(1), [])));
        services.TryAddSingleton<IModelSelector>(
            new ScriptedModelSelector(new InvalidModelPolicy("This test double never selects a model.")));
        services.TryAddSingleton<ILlmModelResolver>(new AliasLlmModelResolver());
        services.TryAddKeyedSingleton<IRunContinuationPolicy>(
            AgentLoopComponentDefaults.ContinuationPolicyKeyValue, new UnsupportedRunContinuationPolicy());
        HookCompositionTestSupport.TryAddDefaultHookKernel(services);
    }

    public static void AddRequiredSecurityServices(IServiceCollection services, bool includeGrantStore = true)
    {
        _ = services.AddAgentPermissions(static options => options.AuditDelivery = SecurityAuditDelivery.BestEffort);
        if (includeGrantStore)
        {
            _ = services.AddInMemorySecurityGrantStore();
        }

        _ = services.AddInMemoryApprovalStore();
        _ = services.AddInMemorySecurityDecisionStore();
        _ = services.AddSecurityAuthority(new ComponentKey<ISecurityAuthority>("authority"));
        _ = services.RemoveAll<ISecurityProfileSelector>();
        _ = services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
    }

    /// <summary>Registers the security services required by facade registration validation.</summary>
    /// <param name="services">The composition under test.</param>
    /// <param name="includeGrantStore">Whether to register the default in-memory grant store.</param>
    public static void AddFacadeRegistrationRequirements(IServiceCollection services, bool includeGrantStore = true) =>
        AddRequiredSecurityServices(services, includeGrantStore);

    public static void AddRequiredSecurityGrantStore(IServiceCollection services) => AddRequiredSecurityServices(services);

    /// <summary>Registers the hook kernel so composition validation can reach later readiness checks.</summary>
    /// <param name="services">The composition under test.</param>
    public static void AddHookKernelForEngineValidation(IServiceCollection services) =>
        HookCompositionTestSupport.TryAddDefaultHookKernel(services);

    public static void AddRunProfiles(IServiceCollection services, params AgentDefinition[] definitions) =>
        AddRunProfiles(services, SessionBusyBehavior.Reject, definitions);

    public static void AddRunProfiles(IServiceCollection services, SessionBusyBehavior busyBehavior, params AgentDefinition[] definitions)
    {
        AddRequiredSecurityServices(services);
        _ = services.RemoveAll<ISecurityProfileSelector>();
        _ = services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        foreach (var definition in definitions)
        {
            _ = services.AddAgentRunProfilePublication(RunProfile(definition, busyBehavior));
        }
    }

    /// <summary>
    /// Builds a composition whose session coordinator is a stateful in-memory double, so engine admission can
    /// create, open, and append to sessions end to end.
    /// </summary>
    public static AgentEngineBuilder SendableBuilder(
        IAgentLoop loop,
        InMemoryTestSessionCoordinator sessions,
        SessionBusyBehavior busyBehavior = SessionBusyBehavior.Reject,
        AgentDefinition? definition = null,
        params AgentDefinition[] definitions)
    {
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.AddKeyedSingleton(AgentLoopComponentDefaults.LoopKeyValue, loop);
        _ = builder.Services.AddSingleton<ISessionCoordinator>(sessions);
        AddRunServicesFakes(builder.Services);
        var selected = definition is not null
            ? [definition, .. definitions]
            : definitions.Length == 0
                ? [Definition()]
                : definitions;
        AddRunProfiles(builder.Services, busyBehavior, selected);
        foreach (var agentDefinition in selected)
        {
            _ = builder.Services.AddAgent(agentDefinition);
        }

        return builder;
    }

    public static ServiceProvider BuildHostedProvider(
        IServiceCollection services,
        ServiceProviderOptions? options = null)
    {
        var factory = options is null
            ? new AgentKitServiceProviderFactory()
            : new AgentKitServiceProviderFactory(options);
        return (ServiceProvider) factory.CreateServiceProvider(factory.CreateBuilder(services));
    }

    /// <summary>
    /// Builds a composition that satisfies engine validation: the facade
    /// defaults, one recording loop, and one published agent.
    /// </summary>
    public static AgentEngineBuilder RunnableBuilder(
        RecordingAgentLoop? loop = null,
        AgentDefinition? definition = null)
    {
        var builder = AgentEngine.CreateBuilder();
        var sessions = new InMemoryTestSessionCoordinator();
        _ = builder.Services.AddSingleton<ISessionCoordinator>(sessions);
        _ = builder.Services.AddKeyedSingleton<IAgentLoop>(
            AgentLoopComponentDefaults.LoopKeyValue, loop ?? new RecordingAgentLoop());
        AddRunServicesFakes(builder.Services);
        var selectedDefinition = definition ?? Definition();
        SeedSession(sessions, selectedDefinition.Id, SessionId);
        AddRunProfiles(builder.Services, selectedDefinition);
        _ = builder.Services.AddAgent(selectedDefinition);
        return builder;
    }
}

/// <summary>
/// An <see cref="IAgentLoop"/> that records the request it received and
/// returns a completed run without contacting anything.
/// </summary>
internal sealed class RecordingAgentLoop: IAgentLoop
{
    /// <summary>Gets every request this loop received, in call order.</summary>
    public List<AgentLoopRunRequest> ReceivedRequests { get; } = [];

    /// <summary>Gets every compiled per-run collaborator bundle this loop received, in call order.</summary>
    public List<AgentRunServices> ReceivedServices { get; } = [];

    public async Task<AgentLoopResult> RunAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);
        cancellationToken.ThrowIfCancellationRequested();
        ReceivedRequests.Add(request);
        ReceivedServices.Add(services);
        var version = await CompositionTestData.CurrentSessionVersionAsync(request, services, cancellationToken)
            .ConfigureAwait(false);

        return new AgentLoopResult(
            request.AgentId,
            request.SessionId,
            request.BranchId,
            request.RunId,
            new RunPolicyHalted(new PolicyHalt(RunResultTestData.Error(AgentErrorCodes.RequestLimit))),
            [],
            version,
            null,
            new RunUsage(request.RunId, []),
            new RunSettlementCompleted());
    }
}

/// <summary>
/// A definition source that publishes a fixed set at a chosen precedence.
/// </summary>
internal sealed class FakeAgentDefinitionSource(
    string sourceId,
    int precedence,
    params AgentDefinition[] definitions): IAgentDefinitionSource
{
    public AgentDefinitionSourceId SourceId { get; } = new(sourceId);

    public ValueTask<AgentDefinitionSourceSnapshot> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new AgentDefinitionSourceSnapshot(
            SourceId,
            new AgentDefinitionSourceVersion(1),
            precedence,
            [.. definitions]));
    }
}
