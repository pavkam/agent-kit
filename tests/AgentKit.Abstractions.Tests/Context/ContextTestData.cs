// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

using AgentKit.Abstractions.Tests.Providers;

/// <summary>Shared valid context contract samples for abstraction tests.</summary>
internal static class ContextTestData
{
    internal static ContextSourceReference Source() =>
        new(new ContextSourceNamespace("agentkit.context"), new ContextSourceKey("sample"), new ContextSourceVersion("1"));

    internal static ContextCandidate Candidate() => new(
        Source(),
        ContextCandidateKind.Instruction,
        ContextTrust.AgentDefinition,
        priority: 0,
        ContextScope.ModelRequest,
        new ContextCostEstimate(8, 2),
        ContextFreshness.Pinned,
        ContextEvaluationFrequency.OncePerModelRequest,
        mandatory: false,
        [],
        ExtensionData.Empty);

    internal static ModelDescriptor Model() => new(
        new ModelAlias("chat"),
        new ProviderId("test-provider"),
        new ApiFamilyId("test-api"),
        new ModelId("test-model"),
        deploymentId: null,
        ProvidersTestData.Capabilities(),
        ProvidersTestData.Limits(),
        pricing: null,
        ExtensionData.Empty);

    internal static ContextContributionRequest ContributionRequest()
    {
        var agentId = new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var sessionId = new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var revision = new AgentDefinitionRevision(1);
        var agent = new AgentDefinition(
            agentId,
            revision,
            "agent",
            new ModelSelectionPolicy([new ModelAlias("chat")]),
            ModelRequirements.None,
            [],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            new RunPolicyDefaults(8, TimeSpan.FromMinutes(1)),
            ExtensionData.Empty,
            new SecurityProfileKey("security"),
            new SessionProfileKey("session"));
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var runId = new RunId(Guid.Parse("50000000-0000-0000-0000-000000000001"));
        var turnId = new TurnId(Guid.Parse("60000000-0000-0000-0000-000000000001"));
        var correlation = new InRunOperationCorrelation(
            new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000001")),
            runId,
            turnId);
        var scope = new SecurityAuthorizationScope(agentId, sessionId, correlation);
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("security"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.Parse("70000000-0000-0000-0000-000000000001")),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("authority"),
            revision,
            new ConfigurationVersion(1),
            scope,
            identity);
        var history = new HistoryView(
            new MessageCursor(agentId, sessionId, null, new BranchId(Guid.Parse("30000000-0000-0000-0000-000000000001")), new SessionVersion(1), new SessionSequence(0)),
            [],
            []);
        var configuration = new EffectiveConfigurationSnapshot(new ConfigurationVersion(1), new ContentHash("sha256:configuration"), [], []);
        return new ContextContributionRequest(
            agent,
            sessionId,
            conversationId: null,
            identity,
            runId,
            turnId,
            new ModelRequestId(Guid.Parse("80000000-0000-0000-0000-000000000001")),
            Model(),
            history,
            authorization,
            configuration);
    }
}
