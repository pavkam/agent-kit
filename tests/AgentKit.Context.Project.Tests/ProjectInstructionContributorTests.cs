// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Project.Tests;

using Microsoft.Extensions.Options;

/// <summary>Tests for <see cref="ProjectInstructionContributor"/>.</summary>
public sealed class ProjectInstructionContributorTests
{
    [Fact]
    public async Task ContributeAsync_WhenAgentsFileExists_ReturnsInstructionCandidate()
    {
        var fileSystem = new StubFileSystem();
        fileSystem.Seed("AGENTS.md", "project rules");
        var contributor = new ProjectInstructionContributor(
            fileSystem,
            new FixedSecurityAuthoritySelector(new AllowAllSecurityAuthority(new InMemoryGrantStore())),
            new GuidSecurityRequestIdGenerator(),
            TimeProvider.System,
            Options.Create(new ProjectInstructionOptions()));
        var request = CreateContributionRequest();

        var contribution = await contributor.ContributeAsync(request, TestContext.Current.CancellationToken);

        contribution.Candidates.Length.ShouldBe(1);
        contribution.Candidates[0].Kind.ShouldBe(ContextCandidateKind.Instruction);
        contribution.Candidates[0].Trust.ShouldBe(ContextTrust.Workspace);
    }

    private static ContextContributionRequest CreateContributionRequest()
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var branchId = new BranchId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var agent = new AgentDefinition(
            agentId,
            new AgentDefinitionRevision(1),
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
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId, turnId);
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("security"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.NewGuid()),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("authority"),
            new AgentDefinitionRevision(1),
            new ConfigurationVersion(1),
            new SecurityAuthorizationScope(agentId, sessionId, correlation),
            identity);
        var user = new UserMessage(
            new MessageId(Guid.NewGuid()),
            agentId,
            sessionId,
            null,
            branchId,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart("hello", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var history = new HistoryView(
            new MessageCursor(agentId, sessionId, null, branchId, new SessionVersion(1), new SessionSequence(0)),
            [user],
            []);
        var configuration = new EffectiveConfigurationSnapshot(
            new ConfigurationVersion(1),
            new ContentHash("sha256:configuration"),
            [],
            []);
        return new ContextContributionRequest(
            agent,
            sessionId,
            conversationId: null,
            identity,
            runId,
            turnId,
            new ModelRequestId(Guid.NewGuid()),
            new ModelDescriptor(
                new ModelAlias("chat"),
                new ProviderId("test"),
                new ApiFamilyId("test"),
                new ModelId("test"),
                null,
                new ModelCapabilities(true, true, true, true, true, true, true, ExtensionData.Empty),
                new ModelLimits(4096, 1024),
                null,
                ExtensionData.Empty),
            history,
            authorization,
            configuration);
    }

    private sealed class InMemoryGrantStore: ISecurityGrantStore
    {
        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new GrantConsumptionResult(GrantConsumptionStatus.Consumed, 0, "Consumed."));

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            SecurityEnforcementIntent intent,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new GrantConsumptionResult(GrantConsumptionStatus.Consumed, 0, "Consumed."));

        public ValueTask<GrantRevocationResult> RevokeAsync(
            GrantId grantId,
            RevocationReason reason,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<GrantRevocationResult>(new GrantRevoked(grantId, reason));
    }
}
