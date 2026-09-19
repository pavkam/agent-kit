// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

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

    /// <summary>
    /// Registers unbehavioral placeholder collaborators for every part of the compiled
    /// <see cref="AgentRunServices"/> bundle a scripted <see cref="IAgentLoop"/> like <see cref="RecordingAgentLoop"/>
    /// never actually reaches, so <see cref="AgentEngine.RunAgentAsync"/> can compile the bundle without throwing.
    /// </summary>
    public static void AddRunServicesFakes(IServiceCollection services)
    {
        services.TryAddSingleton<ISessionCoordinator, UnsupportedSessionCoordinator>();
        services.TryAddSingleton<ISessionRunCoordinator, UnsupportedSessionRunCoordinator>();
        services.TryAddSingleton<IContextAssembler, UnsupportedContextAssembler>();
        services.TryAddSingleton<IToolInvoker, CaptureTestToolInvoker>();
        services.TryAddSingleton<IModelCatalog>(
            new StaticModelCatalog(new ModelCatalogSnapshot(new ModelCatalogVersion(1), [])));
        services.TryAddSingleton<IModelSelector>(
            new ScriptedModelSelector(new InvalidModelPolicy("This test double never selects a model.")));
        services.TryAddSingleton<ILlmModelResolver>(new AliasLlmModelResolver());
        services.TryAddKeyedSingleton<IRunContinuationPolicy>(
            AgentLoopComponentDefaults.ContinuationPolicyKeyValue, new UnsupportedRunContinuationPolicy());
    }

    public static void AddRequiredSecurityGrantStore(IServiceCollection services) =>
        services.TryAddSingleton<ISecurityGrantStore>(static _ =>
            throw new InvalidOperationException("The reduced facade fixture must not activate security grant storage."));

    public static void AddRunProfiles(IServiceCollection services, params AgentDefinition[] definitions) =>
        AddRunProfiles(services, SessionBusyBehavior.Reject, definitions);

    public static void AddRunProfiles(IServiceCollection services, SessionBusyBehavior busyBehavior, params AgentDefinition[] definitions)
    {
        _ = services.AddSingleton<ISecurityProfileSelector>(new TestSecurityProfileSelector());
        AddRequiredSecurityGrantStore(services);
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
        params AgentDefinition[] definitions)
    {
        var builder = AgentEngine.CreateBuilder();
        _ = builder.Services.AddKeyedSingleton(AgentLoopComponentDefaults.LoopKeyValue, loop);
        _ = builder.Services.AddSingleton<ISessionCoordinator>(sessions);
        AddRunServicesFakes(builder.Services);
        var selected = definitions.Length == 0 ? [Definition()] : definitions;
        AddRunProfiles(builder.Services, busyBehavior, selected);
        foreach (var definition in selected)
        {
            _ = builder.Services.AddAgent(definition);
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
        _ = builder.Services.AddKeyedSingleton<IAgentLoop>(
            AgentLoopComponentDefaults.LoopKeyValue, loop ?? new RecordingAgentLoop());
        AddRunServicesFakes(builder.Services);
        var selectedDefinition = definition ?? Definition();
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

    public Task<AgentLoopResult> RunAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);
        cancellationToken.ThrowIfCancellationRequested();
        ReceivedRequests.Add(request);
        ReceivedServices.Add(services);

        return Task.FromResult(new AgentLoopResult(
            request.AgentId,
            request.SessionId,
            request.BranchId,
            request.RunId,
            new RunPolicyHalted(new PolicyHalt(RunResultTestData.Error(AgentErrorCodes.RequestLimit))),
            [],
            new SessionVersion(0),
            null,
            new RunUsage(request.RunId, []),
            new RunSettlementCompleted()));
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
