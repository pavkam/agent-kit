// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextAssemblyRequest"/> evidence-bound construction.</summary>
public sealed class ContextAssemblyRequestTests
{
    [Fact]
    public void Constructor_WhenEvidenceIsNull_ThrowsBeforeDerivingHistory() =>
        Should.Throw<ArgumentNullException>(() => new ContextAssemblyRequest(default, default, default, default, default, default, Model(), null!, [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty)).ParamName.ShouldBe("evidence");

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var evidence = CreateEvidence();
        var model = Model();
        var toolChoice = LlmToolChoice.Auto;
        var settings = LlmRequestSettings.Default;
        var extensions = ExtensionData.Empty;

        var request = Request(evidence, model, [], toolChoice, settings, extensions);

        request.AgentId.ShouldBe(evidence.Agent.Id);
        request.SessionId.ShouldBe(evidence.History.SourceCursor.SessionId);
        request.BranchId.ShouldBe(evidence.History.SourceCursor.BranchId);
        request.RunId.ShouldBe(Correlation(evidence).RunId);
        request.TurnId.ShouldBe(Correlation(evidence).TurnId!.Value);
        request.ModelRequestId.ShouldBe(ModelRequestId());
        request.Model.ShouldBeSameAs(model);
        request.Evidence.ShouldBeSameAs(evidence);
        request.History.ShouldBe(evidence.History.Messages);
        request.Tools.ShouldBeEmpty();
        request.ToolChoice.ShouldBe(toolChoice);
        request.Settings.ShouldBe(settings);
        request.Extensions.ShouldBe(extensions);
        request.Output.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenModelIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Request(CreateEvidence(), null!, [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("model");
    }

    [Fact]
    public void Constructor_WhenToolsAreDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => Request(CreateEvidence(), Model(), default, LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("tools");
    }

    [Fact]
    public void Constructor_WhenToolChoiceIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Request(CreateEvidence(), Model(), [], null!, LlmRequestSettings.Default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("toolChoice");
    }

    [Fact]
    public void Constructor_WhenSettingsIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Request(CreateEvidence(), Model(), [], LlmToolChoice.Auto, null!, ExtensionData.Empty));
        exception.ParamName.ShouldBe("settings");
    }

    [Fact]
    public void Constructor_WhenExtensionsIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Request(CreateEvidence(), Model(), [], LlmToolChoice.Auto, LlmRequestSettings.Default, null!));
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
        var correlation = Correlation(evidence);
        var otherId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var exception = Should.Throw<ArgumentException>(() => new ContextAssemblyRequest(
            mismatch == "agentId" ? new AgentId(otherId) : evidence.Agent.Id,
            mismatch == "sessionId" ? new SessionId(otherId) : evidence.History.SourceCursor.SessionId,
            mismatch == "branchId" ? new BranchId(otherId) : evidence.History.SourceCursor.BranchId,
            mismatch == "runId" ? new RunId(otherId) : correlation.RunId,
            mismatch == "turnId" ? new TurnId(otherId) : correlation.TurnId!.Value,
            ModelRequestId(), Model(), evidence, [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty));
        exception.ParamName.ShouldBe(mismatch);
    }

    [Fact]
    public void Constructor_WhenEvidenceCorrelationIsNotInRun_ThrowsExactParameter()
    {
        var agentId = new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var sessionId = new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var revision = new AgentDefinitionRevision(1);
        var agent = TestSupport.AgentDefinitionFixtures.Create(agentId, revision: revision.Value, displayName: "agent");
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000001")), null);
        var authorization = new SecurityAuthorizationContext(new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("70000000-0000-0000-0000-000000000001")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), revision, new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);
        var history = new HistoryView(new MessageCursor(agentId, sessionId, null, new BranchId(Guid.Parse("30000000-0000-0000-0000-000000000001")), new SessionVersion(1), new SessionSequence(0)), [], []);
        var configuration = new EffectiveConfigurationSnapshot(new ConfigurationVersion(1), new ContentHash("sha256:configuration"), [], []);
        var evidence = new ContextAssemblyEvidence(agent, identity, history, authorization, configuration);

        var exception = Should.Throw<ArgumentException>(() => new ContextAssemblyRequest(agentId, sessionId, evidence.History.SourceCursor.BranchId, default, default, ModelRequestId(), Model(), evidence, [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("evidence");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var evidence = CreateEvidence();
        var first = Request(evidence, Model(), [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        var second = Request(evidence, Model(), [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenEvidenceHistoryDiffers_IsNotEqual()
    {
        var evidence = CreateEvidence();
        var other = new ContextAssemblyEvidence(
            evidence.Agent,
            evidence.Identity,
            new HistoryView(evidence.History.SourceCursor, [HistoryMessage(evidence)], []),
            evidence.Authorization,
            evidence.Configuration);

        Request(evidence, Model(), [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty)
            .ShouldNotBe(Request(other, Model(), [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Request(CreateEvidence(), Model(), [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Equals_WhenHistoryAndToolsAreNonEmpty_HasEqualHashCode()
    {
        var source = CreateEvidence();
        var evidence = new ContextAssemblyEvidence(
            source.Agent,
            source.Identity,
            new HistoryView(source.History.SourceCursor, [HistoryMessage(source)], []),
            source.Authorization,
            source.Configuration);
        var tool = new LlmToolDefinition(new ToolId("tool"), "tool", null, default);
        var first = Request(evidence, Model(), [tool], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        var second = Request(evidence, Model(), [tool], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    private static ModelRequestId ModelRequestId() => new(Guid.Parse("60000000-0000-0000-0000-000000000006"));

    private static ModelDescriptor Model() => new(new ModelAlias("chat"), new ProviderId("provider"), new ApiFamilyId("api"), new ModelId("model"), null, new ModelCapabilities(true, true, true, true, true, true, true, ExtensionData.Empty), new ModelLimits(4096, 1024), null, ExtensionData.Empty);

    private static InRunOperationCorrelation Correlation(ContextAssemblyEvidence evidence) =>
        (InRunOperationCorrelation) evidence.Authorization.Scope.Correlation;

    private static ContextAssemblyRequest Request(
        ContextAssemblyEvidence evidence,
        ModelDescriptor model,
        ImmutableArray<LlmToolDefinition> tools,
        LlmToolChoice toolChoice,
        LlmRequestSettings settings,
        ExtensionData extensions) => new(
        evidence.Agent.Id,
        evidence.History.SourceCursor.SessionId,
        evidence.History.SourceCursor.BranchId,
        Correlation(evidence).RunId,
        Correlation(evidence).TurnId!.Value,
        ModelRequestId(),
        model,
        evidence,
        tools,
        toolChoice,
        settings,
        extensions);

    private static UserMessage HistoryMessage(ContextAssemblyEvidence evidence) => new UserMessage(
        new MessageId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
        evidence.Agent.Id,
        evidence.History.SourceCursor.SessionId,
        null,
        evidence.History.SourceCursor.BranchId,
        Correlation(evidence).RunId,
        Correlation(evidence).TurnId,
        DateTimeOffset.UnixEpoch,
        MessageState.Complete,
        [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)],
        ExtensionData.Empty);

    private static ContextAssemblyEvidence CreateEvidence()
    {
        var agentId = new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var sessionId = new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var revision = new AgentDefinitionRevision(1);
        var agent = TestSupport.AgentDefinitionFixtures.Create(agentId, revision: revision.Value, displayName: "agent");
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000001")), new RunId(Guid.Parse("50000000-0000-0000-0000-000000000001")), new TurnId(Guid.Parse("60000000-0000-0000-0000-000000000001")));
        var authorization = new SecurityAuthorizationContext(new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("70000000-0000-0000-0000-000000000001")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"), revision, new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);
        var history = new HistoryView(new MessageCursor(agentId, sessionId, null, new BranchId(Guid.Parse("30000000-0000-0000-0000-000000000001")), new SessionVersion(1), new SessionSequence(0)), [], []);
        var configuration = new EffectiveConfigurationSnapshot(new ConfigurationVersion(1), new ContentHash("sha256:configuration"), [], []);
        return new ContextAssemblyEvidence(agent, identity, history, authorization, configuration);
    }
}
