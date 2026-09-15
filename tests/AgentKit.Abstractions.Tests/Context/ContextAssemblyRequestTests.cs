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
