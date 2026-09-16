// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextAssemblyRequest"/> evidence-aware construction.</summary>
public sealed class ContextAssemblyRequestTests
{
    [Fact]
    public void Constructor_WhenEvidenceIsNull_ThrowsBeforeDerivingHistory() =>
        Should.Throw<ArgumentNullException>(() => new ContextAssemblyRequest(default, default, default, default, default, default, null!, [], null!, [], null!, null!, null!)).ParamName.ShouldBe("evidence");

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var model = Model();
        var toolChoice = LlmToolChoice.Auto;
        var settings = LlmRequestSettings.Default;
        var extensions = ExtensionData.Empty;
        var request = new ContextAssemblyRequest(AgentId(), SessionId(), BranchId(), RunId(), TurnId(), ModelRequestId(), model, [], [], [], toolChoice, settings, extensions);
        request.AgentId.ShouldBe(AgentId());
        request.SessionId.ShouldBe(SessionId());
        request.BranchId.ShouldBe(BranchId());
        request.RunId.ShouldBe(RunId());
        request.TurnId.ShouldBe(TurnId());
        request.ModelRequestId.ShouldBe(ModelRequestId());
        request.Model.ShouldBeSameAs(model);
        request.Instructions.ShouldBeEmpty();
        request.History.ShouldBeEmpty();
        request.Evidence.ShouldBeNull();
        request.Tools.ShouldBeEmpty();
        request.ToolChoice.ShouldBe(toolChoice);
        request.Settings.ShouldBe(settings);
        request.Extensions.ShouldBe(extensions);
    }

    [Fact]
    public void Constructor_WhenModelIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ContextAssemblyRequest(AgentId(), SessionId(), BranchId(), RunId(), TurnId(), ModelRequestId(), null!, [], [], [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("model");
    }

    [Fact]
    public void Constructor_WhenInstructionsAreDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ContextAssemblyRequest(AgentId(), SessionId(), BranchId(), RunId(), TurnId(), ModelRequestId(), Model(), default, [], [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("instructions");
    }

    [Fact]
    public void Constructor_WhenHistoryIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ContextAssemblyRequest(AgentId(), SessionId(), BranchId(), RunId(), TurnId(), ModelRequestId(), Model(), [], default(ImmutableArray<AgentMessage>), [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("history");
    }

    [Fact]
    public void Constructor_WhenToolsAreDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ContextAssemblyRequest(AgentId(), SessionId(), BranchId(), RunId(), TurnId(), ModelRequestId(), Model(), [], [], default, LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("tools");
    }

    [Fact]
    public void Constructor_WhenToolChoiceIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ContextAssemblyRequest(AgentId(), SessionId(), BranchId(), RunId(), TurnId(), ModelRequestId(), Model(), [], [], [], null!, LlmRequestSettings.Default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("toolChoice");
    }

    [Fact]
    public void Constructor_WhenSettingsIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ContextAssemblyRequest(AgentId(), SessionId(), BranchId(), RunId(), TurnId(), ModelRequestId(), Model(), [], [], [], LlmToolChoice.Auto, null!, ExtensionData.Empty));
        exception.ParamName.ShouldBe("settings");
    }

    [Fact]
    public void Constructor_WhenExtensionsIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ContextAssemblyRequest(AgentId(), SessionId(), BranchId(), RunId(), TurnId(), ModelRequestId(), Model(), [], [], [], LlmToolChoice.Auto, LlmRequestSettings.Default, null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Theory]
    [InlineData("agentId")]
    [InlineData("sessionId")]
    [InlineData("branchId")]
    [InlineData("runId")]
    [InlineData("turnId")]
    public void Constructor_WhenEvidenceCoordinatesDisagree_ThrowsExactParameter(string mismatch)
    {
        var evidence = CreateEvidence();
        var correlation = (InRunOperationCorrelation) evidence.Authorization.Scope.Correlation;
        var otherId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var exception = Should.Throw<ArgumentException>(() => new ContextAssemblyRequest(
            mismatch == "agentId" ? new AgentId(otherId) : evidence.Agent.Id,
            mismatch == "sessionId" ? new SessionId(otherId) : evidence.History.SourceCursor.SessionId,
            mismatch == "branchId" ? new BranchId(otherId) : evidence.History.SourceCursor.BranchId,
            mismatch == "runId" ? new RunId(otherId) : correlation.RunId,
            mismatch == "turnId" ? new TurnId(otherId) : correlation.TurnId!.Value,
            ModelRequestId(), Model(), [], evidence, [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty));
        exception.ParamName.ShouldBe(mismatch);
    }

    [Fact]
    public void Constructor_WhenEvidenceCorrelationIsNotInRun_ThrowsExactParameter()
    {
        var agentId = new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var sessionId = new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var revision = new AgentDefinitionRevision(1);
        var agent = new AgentDefinition(agentId, revision, "agent", new ModelSelectionPolicy([new ModelAlias("chat")]), ModelRequirements.None, [], [], LlmToolChoice.Auto, LlmRequestSettings.Default, new RunPolicyDefaults(8, TimeSpan.FromMinutes(1)), ExtensionData.Empty, new SecurityProfileKey("security"), new SessionProfileKey("session"));
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000001")), null);
        var authorization = new SecurityAuthorizationContext(new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("70000000-0000-0000-0000-000000000001")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), revision, new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);
        var history = new HistoryView(new MessageCursor(agentId, sessionId, null, new BranchId(Guid.Parse("30000000-0000-0000-0000-000000000001")), new SessionVersion(1), new SessionSequence(0)), [], []);
        var configuration = new EffectiveConfigurationSnapshot(new ConfigurationVersion(1), new ContentHash("sha256:configuration"), [], []);
        var evidence = new ContextAssemblyEvidence(agent, identity, history, authorization, configuration);

        var exception = Should.Throw<ArgumentException>(() => new ContextAssemblyRequest(agentId, sessionId, evidence.History.SourceCursor.BranchId, default, default, ModelRequestId(), Model(), [], evidence, [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("evidence");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new ContextAssemblyRequest(AgentId(), SessionId(), BranchId(), RunId(), TurnId(), ModelRequestId(), Model(), [], [], [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        var second = new ContextAssemblyRequest(AgentId(), SessionId(), BranchId(), RunId(), TurnId(), ModelRequestId(), Model(), [], [], [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenEvidenceDiffers_IsNotEqual()
    {
        var evidence = CreateEvidence();
        var correlation = (InRunOperationCorrelation) evidence.Authorization.Scope.Correlation;
        var withEvidence = new ContextAssemblyRequest(evidence.Agent.Id, evidence.History.SourceCursor.SessionId, evidence.History.SourceCursor.BranchId, correlation.RunId, correlation.TurnId!.Value, ModelRequestId(), Model(), [], evidence, [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        var withoutEvidence = new ContextAssemblyRequest(evidence.Agent.Id, evidence.History.SourceCursor.SessionId, evidence.History.SourceCursor.BranchId, correlation.RunId, correlation.TurnId!.Value, ModelRequestId(), Model(), [], [], [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        withEvidence.ShouldNotBe(withoutEvidence);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ContextAssemblyRequest(AgentId(), SessionId(), BranchId(), RunId(), TurnId(), ModelRequestId(), Model(), [], [], [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Equals_WhenInstructionsHistoryAndToolsAreNonEmpty_HasEqualHashCode()
    {
        AgentMessage instruction = new UserMessage(new MessageId(Guid.Parse("70000000-0000-0000-0000-000000000007")), AgentId(), SessionId(), null, BranchId(), null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [new TextPart("system", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        AgentMessage historyMessage = new UserMessage(new MessageId(Guid.Parse("80000000-0000-0000-0000-000000000008")), AgentId(), SessionId(), null, BranchId(), RunId(), TurnId(), DateTimeOffset.UnixEpoch, MessageState.Complete, [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var tool = new LlmToolDefinition(new ToolId("tool"), "tool", null, default);
        var first = new ContextAssemblyRequest(AgentId(), SessionId(), BranchId(), RunId(), TurnId(), ModelRequestId(), Model(), [instruction], [historyMessage], [tool], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        var second = new ContextAssemblyRequest(AgentId(), SessionId(), BranchId(), RunId(), TurnId(), ModelRequestId(), Model(), [instruction], [historyMessage], [tool], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    private static AgentId AgentId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static SessionId SessionId() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    private static BranchId BranchId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static RunId RunId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static TurnId TurnId() => new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    private static ModelRequestId ModelRequestId() => new(Guid.Parse("60000000-0000-0000-0000-000000000006"));
    private static ModelDescriptor Model() => new(new ModelAlias("chat"), new ProviderId("provider"), new ApiFamilyId("api"), new ModelId("model"), null, new ModelCapabilities(true, true, true, true, true, true, true, ExtensionData.Empty), new ModelLimits(4096, 1024), null, ExtensionData.Empty);

    [Fact]
    public void Constructor_WhenEvidenceCoordinatesAgree_DerivesExactHistory()
    {
        var evidence = CreateEvidence();
        var correlation = (InRunOperationCorrelation) evidence.Authorization.Scope.Correlation;
        var capabilities = new ModelCapabilities(true, true, true, true, true, true, true, ExtensionData.Empty);
        var model = new ModelDescriptor(new ModelAlias("chat"), new ProviderId("provider"), new ApiFamilyId("api"), new ModelId("model"), null, capabilities, new ModelLimits(4096, 1024), null, ExtensionData.Empty);

        var request = new ContextAssemblyRequest(evidence.Agent.Id, evidence.History.SourceCursor.SessionId, evidence.History.SourceCursor.BranchId, correlation.RunId, correlation.TurnId!.Value, new ModelRequestId(Guid.NewGuid()), model, [], evidence, [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);

        request.Evidence.ShouldBeSameAs(evidence);
        request.History.ShouldBe(evidence.History.Messages);
    }

    private static ContextAssemblyEvidence CreateEvidence()
    {
        var agentId = new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var sessionId = new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var revision = new AgentDefinitionRevision(1);
        var agent = new AgentDefinition(agentId, revision, "agent", new ModelSelectionPolicy([new ModelAlias("chat")]), ModelRequirements.None, [], [], LlmToolChoice.Auto, LlmRequestSettings.Default, new RunPolicyDefaults(8, TimeSpan.FromMinutes(1)), ExtensionData.Empty, new SecurityProfileKey("security"), new SessionProfileKey("session"));
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000001")), new RunId(Guid.Parse("50000000-0000-0000-0000-000000000001")), new TurnId(Guid.Parse("60000000-0000-0000-0000-000000000001")));
        var authorization = new SecurityAuthorizationContext(new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("70000000-0000-0000-0000-000000000001")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), revision, new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);
        var history = new HistoryView(new MessageCursor(agentId, sessionId, null, new BranchId(Guid.Parse("30000000-0000-0000-0000-000000000001")), new SessionVersion(1), new SessionSequence(0)), [], []);
        var configuration = new EffectiveConfigurationSnapshot(new ConfigurationVersion(1), new ContentHash("sha256:configuration"), [], []);
        return new ContextAssemblyEvidence(agent, identity, history, authorization, configuration);
    }
}
