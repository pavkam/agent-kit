// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Collections.Immutable;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="DefinitionCompositionValidator"/> through composition build.</summary>
public sealed class DefinitionCompositionValidatorTests
{
    private static readonly ToolsetKey _toolsetKey = new("set");

    private static readonly ImmutableArray<ToolsetReference> _toolsets =
        [new ToolsetReference(_toolsetKey, new ToolExecutionPolicyKey("policy"))];

    [Theory]
    [InlineData("continuation-policy")]
    [InlineData("input-coordinator")]
    [InlineData("output-publisher")]
    [InlineData("output-processor")]
    [InlineData("context-assembler")]
    [InlineData("model-selector")]
    [InlineData("model-executor")]
    public void Build_WhenARequiredSelectedKeyHasNoRegistration_RejectsWithThatRoleMissing(string role)
    {
        var builder = CompositionTestData.RunnableBuilder();
        Roles[role].Remove(builder.Services);

        Codes(builder).ShouldContain($"agentkit.definition.{role}.missing");
    }

    [Theory]
    [InlineData("continuation-policy")]
    [InlineData("input-coordinator")]
    [InlineData("output-publisher")]
    [InlineData("output-processor")]
    [InlineData("context-assembler")]
    [InlineData("model-selector")]
    [InlineData("model-executor")]
    public void Build_WhenARequiredSelectedKeyHasTwoRegistrations_RejectsWithThatRoleAmbiguous(string role)
    {
        var builder = CompositionTestData.RunnableBuilder();
        Roles[role].Duplicate(builder.Services);

        Codes(builder).ShouldContain($"agentkit.definition.{role}.ambiguous");
    }

    [Theory]
    [InlineData("continuation-policy")]
    [InlineData("input-coordinator")]
    [InlineData("output-publisher")]
    [InlineData("output-processor")]
    [InlineData("context-assembler")]
    [InlineData("model-selector")]
    [InlineData("model-executor")]
    public void Build_WhenAnUnkeyedRegistrationExistsButTheSelectedKeyIsAbsent_StillRejects(string role)
    {
        var builder = CompositionTestData.RunnableBuilder();
        Roles[role].Remove(builder.Services);
        Roles[role].AddUnkeyed(builder.Services);

        Codes(builder).ShouldContain($"agentkit.definition.{role}.missing");
    }

    [Fact]
    public void Build_WhenLoopKeyIsAbsent_RejectsWithLoopMissing()
    {
        var definition = CompositionTestData.Definition().WithComponents(loop: new ComponentKey<IAgentLoop>("absent"));
        var builder = CompositionTestData.RunnableBuilder(definition: definition);

        Codes(builder).ShouldContain("agentkit.definition.loop.missing");
    }

    [Fact]
    public void Build_WhenThePublishedSecurityAuthorityIsNotInstalled_RejectsWithSecurityAuthorityMissing()
    {
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<ISecurityAuthorityCatalog>();
        _ = builder.Services.AddSingleton<ISecurityAuthorityCatalog>(new StaticSecurityAuthorityCatalog());

        Codes(builder).ShouldContain("agentkit.definition.security-authority.missing");
    }

    [Fact]
    public async Task Build_WhenNoSecurityAuthorityCatalogIsComposed_SkipsTheBuildTimeAuthorityKeyCheck()
    {
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<ISecurityAuthorityCatalog>();

        await using var engine = builder.Build();

        _ = engine;
    }

    [Fact]
    public void Build_WhenSelectedBudgetProfileIsNotInTheCatalog_RejectsWithBudgetProfileMissing()
    {
        var definition = CompositionTestData.Definition().WithComponents(budgetProfile: new BudgetProfileKey("absent"));
        var builder = CompositionTestData.RunnableBuilder(definition: definition);

        Codes(builder).ShouldContain("agentkit.definition.budget-profile.missing");
    }

    [Fact]
    public void Build_WhenSessionCoordinatorIsNotRegistered_RejectsWithCollaboratorMissing()
    {
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<ISessionCoordinator>();

        Codes(builder).ShouldContain("agentkit.definition.collaborator.missing");
    }

    [Fact]
    public void Build_WhenModelResolverIsNotRegistered_RejectsWithCollaboratorMissing()
    {
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<ILlmModelResolver>();

        Codes(builder).ShouldContain("agentkit.definition.collaborator.missing");
    }

    [Fact]
    public void Build_WhenRunCoordinatorIsNotRegistered_RejectsWithRunCoordinatorMissing()
    {
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<ISessionRunCoordinator>();

        Codes(builder).ShouldContain("agentkit.definition.run-coordinator.missing");
    }

    [Fact]
    public void Build_WhenTheSessionProfilesStoreIsNotCatalogued_RejectsWithSessionStoreMissing()
    {
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<ISessionStoreCatalog>();
        _ = builder.Services.AddSingleton<ISessionStoreCatalog>(new StaticSessionStoreCatalog());

        Codes(builder).ShouldContain("agentkit.definition.session-store.missing");
    }

    [Fact]
    public void Build_WhenTheSessionProfileRequiresADurableStoreTheCatalogedOneIsNot_RejectsWithSessionStoreIncompatible()
    {
        var definition = CompositionTestData.Definition();
        var builder = AgentEngine.CreateBuilder();
        var sessions = new InMemoryTestSessionCoordinator();
        _ = builder.Services.AddSingleton<ISessionCoordinator>(sessions);
        _ = builder.Services.AddKeyedSingleton<IAgentLoop>(AgentLoopComponentDefaults.LoopKeyValue, new RecordingAgentLoop());
        CompositionTestData.AddRunServicesFakes(builder.Services);
        CompositionTestData.AddRequiredSecurityServices(builder.Services);
        _ = builder.Services.AddAgentRunProfilePublication(CompositionTestData.RunProfile(definition, SessionBusyBehavior.Reject, requiresDurableStore: true));
        _ = builder.Services.AddAgent(definition);

        Codes(builder).ShouldContain("agentkit.definition.session-store.incompatible");
    }

    [Fact]
    public void Build_WhenNoCandidateIsACatalogedConversationModel_RejectsWithModelMissing()
    {
        var definition = CompositionTestData.Definition() with { Models = new ModelSelectionPolicy([new ModelAlias("absent")]) };
        var builder = CompositionTestData.RunnableBuilder(definition: definition);

        Codes(builder).ShouldContain("agentkit.definition.model.missing");
    }

    [Fact]
    public void Build_WhenOnlyALaterCandidateIsCataloged_Accepts()
    {
        var definition = CompositionTestData.Definition() with { Models = new ModelSelectionPolicy([new ModelAlias("absent"), new ModelAlias("chat")]) };
        var builder = CompositionTestData.RunnableBuilder(definition: definition);

        Should.NotThrow(() => builder.Build().DisposeAsync().AsTask().GetAwaiter().GetResult());
    }

    [Fact]
    public void Build_WhenTheModelCatalogSnapshotCannotBeRead_RejectsWithModelCatalogUnavailable()
    {
        var builder = CompositionTestData.RunnableBuilder();
        _ = builder.Services.RemoveAll<IModelCatalog>();
        _ = builder.Services.AddSingleton<IModelCatalog>(new ThrowingModelCatalog());

        Codes(builder).ShouldContain("agentkit.definition.model-catalog-unavailable");
    }

    [Fact]
    public void Build_WhenToolsetsAreSelectedWithoutAToolExecutor_RejectsWithExecutorMissing()
    {
        var definition = AgentDefinitionFixtures.Create(CompositionTestData.AgentId, toolsets: _toolsets);
        var builder = CompositionTestData.RunnableBuilder(definition: definition);
        _ = builder.Services.AddKeyedSingleton(_toolsetKey, ToolCatalogMergeTestData.Toolset("set", [], []));

        Codes(builder).ShouldContain("agentkit.definition.toolsets.executor-missing");
    }

    [Fact]
    public void Build_WhenAToolExecutorIsSelectedWithoutToolsets_RejectsWithToolsetsMissing()
    {
        var executorKey = new ComponentKey<IToolExecutor>("tools");
        var definition = AgentDefinitionFixtures.Create(
            CompositionTestData.AgentId,
            optionalCapabilities: new AgentOptionalCapabilitySelection(executorKey, null, null, null, null, []));
        var builder = CompositionTestData.RunnableBuilder(definition: definition);
        _ = builder.Services.AddKeyedSingleton<IToolExecutor>(executorKey.Value, new CaptureTestToolExecutor());

        Codes(builder).ShouldContain("agentkit.definition.tool-executor.toolsets-missing");
    }

    [Fact]
    public void Build_WhenTheSelectedToolExecutorKeyHasNoKeyedRegistration_RejectsEvenWithAnUnkeyedExecutor()
    {
        var executorKey = new ComponentKey<IToolExecutor>("tools");
        var definition = AgentDefinitionFixtures.Create(
            CompositionTestData.AgentId,
            toolsets: _toolsets,
            optionalCapabilities: new AgentOptionalCapabilitySelection(executorKey, null, null, null, null, []));
        var builder = CompositionTestData.RunnableBuilder(definition: definition);
        _ = builder.Services.AddKeyedSingleton(_toolsetKey, ToolCatalogMergeTestData.Toolset("set", [], []));

        Codes(builder).ShouldContain("agentkit.definition.tool-executor.missing");
    }

    [Fact]
    public void Build_WhenAToolsetHasNoRegisteredPublication_RejectsWithToolsetMissing()
    {
        var executorKey = new ComponentKey<IToolExecutor>("tools");
        var definition = AgentDefinitionFixtures.Create(
            CompositionTestData.AgentId,
            toolsets: _toolsets,
            optionalCapabilities: new AgentOptionalCapabilitySelection(executorKey, null, null, null, null, []));
        var builder = CompositionTestData.RunnableBuilder(definition: definition);
        _ = builder.Services.AddKeyedSingleton<IToolExecutor>(executorKey.Value, new CaptureTestToolExecutor());

        Codes(builder).ShouldContain("agentkit.definition.toolset.missing");
    }

    [Fact]
    public async Task Build_WhenToolsetsExecutorAndPublicationAgree_Accepts()
    {
        var executorKey = new ComponentKey<IToolExecutor>("tools");
        var definition = AgentDefinitionFixtures.Create(
            CompositionTestData.AgentId,
            toolsets: _toolsets,
            optionalCapabilities: new AgentOptionalCapabilitySelection(executorKey, null, null, null, null, []));
        var builder = CompositionTestData.RunnableBuilder(definition: definition);
        _ = builder.Services.AddKeyedSingleton<IToolExecutor>(executorKey.Value, new CaptureTestToolExecutor());
        _ = builder.Services.AddKeyedSingleton(_toolsetKey, ToolCatalogMergeTestData.Toolset("set", [], []));

        await using var engine = builder.Build();

        _ = engine;
    }

    [Fact]
    public void Build_WhenACapabilityReferenceHasNoSource_RejectsWithCapabilityUnregistered()
    {
        var builder = CompositionTestData.RunnableBuilder(definition: DefinitionReferencing("cap", "profile"));

        Codes(builder).ShouldContain("agentkit.definition.capability.unregistered");
    }

    [Fact]
    public void Build_WhenTwoSourcesClaimACapability_RejectsWithCapabilityAmbiguous()
    {
        var builder = CompositionTestData.RunnableBuilder(definition: DefinitionReferencing("cap", "profile"));
        _ = builder.Services.AddSingleton<IAgentCapabilityProfileSource>(new StaticAgentCapabilityProfileSource(new CapabilityId("cap"), new CapabilityProfileId("profile")));
        _ = builder.Services.AddSingleton<IAgentCapabilityProfileSource>(new StaticAgentCapabilityProfileSource(new CapabilityId("cap"), new CapabilityProfileId("profile")));

        Codes(builder).ShouldContain("agentkit.definition.capability.ambiguous");
    }

    [Fact]
    public void Build_WhenTheCapabilitySourceDoesNotContainTheProfile_RejectsWithCapabilityProfileMissing()
    {
        var builder = CompositionTestData.RunnableBuilder(definition: DefinitionReferencing("cap", "profile"));
        _ = builder.Services.AddSingleton<IAgentCapabilityProfileSource>(new StaticAgentCapabilityProfileSource(new CapabilityId("cap"), new CapabilityProfileId("other")));

        Codes(builder).ShouldContain("agentkit.definition.capability.profile-missing");
    }

    [Fact]
    public async Task Build_WhenTheCapabilitySourceContainsTheProfile_Accepts()
    {
        var builder = CompositionTestData.RunnableBuilder(definition: DefinitionReferencing("cap", "profile"));
        _ = builder.Services.AddSingleton<IAgentCapabilityProfileSource>(new StaticAgentCapabilityProfileSource(new CapabilityId("cap"), new CapabilityProfileId("profile")));

        await using var engine = builder.Build();

        _ = engine;
    }

    private static Dictionary<string, RoleRegistration> Roles { get; } = new()
    {
        ["continuation-policy"] = RoleRegistration.Create<IRunContinuationPolicy>(AgentLoopComponentDefaults.ContinuationPolicyKeyValue),
        ["input-coordinator"] = RoleRegistration.Create<IInputCoordinator>(AgentIOComponentDefaults.InputCoordinatorKeyValue),
        ["output-publisher"] = RoleRegistration.Create<IOutputPublisher>(AgentIOComponentDefaults.OutputPublisherKeyValue),
        ["output-processor"] = RoleRegistration.Create<IOutputProcessor>(AgentOutputComponentDefaults.ProcessorKeyValue),
        ["context-assembler"] = RoleRegistration.Create<IContextAssembler>(AgentContextComponentDefaults.AssemblerKeyValue),
        ["model-selector"] = RoleRegistration.Create<IModelSelector>(AgentProviderComponentDefaults.ModelSelectorKeyValue),
        ["model-executor"] = RoleRegistration.Create<IModelRequestExecutor>(AgentProviderComponentDefaults.ModelExecutorKeyValue),
    };

    private static AgentDefinition DefinitionReferencing(string capability, string profile) =>
        AgentDefinitionFixtures.Create(
            CompositionTestData.AgentId,
            optionalCapabilities: new AgentOptionalCapabilitySelection(
                null, null, null, null, null, [new AgentCapabilityReference(new CapabilityId(capability), new CapabilityProfileId(profile))]));

    private static string[] Codes(AgentEngineBuilder builder) =>
        [.. Should.Throw<AgentCompositionException>(builder.Build).Diagnostics.Select(static diagnostic => diagnostic.Code)];

    /// <summary>Removes, duplicates, or substitutes the registration a definition selects for one component role.</summary>
    private sealed record RoleRegistration(
        Action<IServiceCollection> Remove,
        Action<IServiceCollection> Duplicate,
        Action<IServiceCollection> AddUnkeyed)
    {
        public static RoleRegistration Create<TService>(string key)
            where TService : class => new(
                static services => _ = services.RemoveAllKeyed<TService>(KeyOf<TService>(services)),
                services => _ = services.AddKeyedSingleton<TService>(key, static (_, _) => throw new InvalidOperationException("Validation must not run duplicate factories.")),
                static services => _ = services.AddSingleton<TService>(static _ => throw new InvalidOperationException("An unkeyed registration must not satisfy a keyed selection.")));

        private static string KeyOf<TService>(IServiceCollection services) =>
            services.Last(static descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(TService)).ServiceKey as string
            ?? throw new InvalidOperationException("The role has no keyed registration to remove.");
    }

    private sealed class StaticSecurityAuthorityCatalog(params ComponentKey<ISecurityAuthority>[] keys): ISecurityAuthorityCatalog
    {
        public bool Contains(ComponentKey<ISecurityAuthority> key) => keys.Contains(key);
    }

    private sealed class ThrowingModelCatalog: IModelCatalog
    {
        public ValueTask<ModelCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The catalog is down.");
    }
}
