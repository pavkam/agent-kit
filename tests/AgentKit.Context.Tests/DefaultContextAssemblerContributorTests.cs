// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Tests;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Contributor execution and manifest behavior for <see cref="DefaultContextAssembler"/>.</summary>
public sealed class DefaultContextAssemblerContributorTests
{
    [Fact]
    public async Task AssembleAsync_WhenContributorReturnsCandidate_AttachesManifestOnReadyContext()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentContext();
        _ = services.AddContextContributor<ReferenceContributor>(
            AgentContextComponentDefaults.AssemblerKey,
            new ContextContributorRegistration(
                new ContextSourceKey("reference"),
                order: 0,
                ContextEvaluationFrequency.OncePerModelRequest,
                required: true));

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var assembler = scope.ServiceProvider.GetRequiredService<IContextAssembler>();
        var request = CreateEvidenceRequest([TestFactory.UserMessage()]);

        var result = await assembler.AssembleAsync(request, TestContext.Current.CancellationToken);

        var ready = result.ShouldBeOfType<ContextReady>();
        _ = ready.Context.Manifest.ShouldNotBeNull();
        ready.Context.Manifest!.Entries.Length.ShouldBe(1);
        ready.Context.Manifest.Entries[0].Disposition.ShouldBe(ContextManifestDisposition.Included);
    }

    [Fact]
    public async Task AssembleAsync_WhenContributorProposesUntrustedInstruction_FailsClosed()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentContext();
        _ = services.AddContextContributor<UntrustedInstructionContributor>(
            AgentContextComponentDefaults.AssemblerKey,
            new ContextContributorRegistration(
                new ContextSourceKey("bad"),
                order: 0,
                ContextEvaluationFrequency.OncePerModelRequest,
                required: true));

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var assembler = scope.ServiceProvider.GetRequiredService<IContextAssembler>();
        var request = CreateEvidenceRequest([TestFactory.UserMessage()]);

        var result = await assembler.AssembleAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ContextPreparationFailed>();
    }

    [Fact]
    public async Task AssembleAsync_WhenContributorIsOncePerRun_InvokesOnlyOnFirstModelRequestInScope()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentContext();
        _ = services.AddContextContributor<CountingContributor>(
            AgentContextComponentDefaults.AssemblerKey,
            new ContextContributorRegistration(
                new ContextSourceKey("counter"),
                order: 0,
                ContextEvaluationFrequency.OncePerRun,
                required: true));

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var assembler = scope.ServiceProvider.GetRequiredService<IContextAssembler>();
        var history = ImmutableArray<AgentMessage>.Empty.Add(TestFactory.UserMessage());

        CountingContributor.Reset();
        _ = await assembler.AssembleAsync(CreateEvidenceRequest(history), TestContext.Current.CancellationToken);
        _ = await assembler.AssembleAsync(CreateEvidenceRequest(history), TestContext.Current.CancellationToken);

        CountingContributor.InvocationCount.ShouldBe(1);
    }

    private static ImmutableArray<AgentMessage> AlignHistory(
        ImmutableArray<AgentMessage> history,
        AgentId agentId,
        SessionId sessionId,
        BranchId branchId)
    {
        if (history.IsDefaultOrEmpty)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<AgentMessage>(history.Length);
        foreach (var message in history)
        {
            builder.Add(message switch
            {
                UserMessage user => new UserMessage(
                    user.Id,
                    agentId,
                    sessionId,
                    user.ConversationId,
                    branchId,
                    user.RunId,
                    user.TurnId,
                    user.CreatedAt,
                    user.State,
                    user.Parts,
                    user.Extensions),
                _ => throw new NotSupportedException("Contributor tests align only user history messages."),
            });
        }

        return builder.ToImmutable();
    }

    private static ContextAssemblyRequest CreateEvidenceRequest(ImmutableArray<AgentMessage> history)
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var branchId = new BranchId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
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
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human);
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId, turnId);
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("security"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.NewGuid()),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("authority"),
            revision,
            new ConfigurationVersion(1),
            new SecurityAuthorizationScope(agentId, sessionId, correlation),
            identity);
        var alignedHistory = AlignHistory(history, agentId, sessionId, branchId);
        var historyView = new HistoryView(
            new MessageCursor(agentId, sessionId, null, branchId, new SessionVersion(1), new SessionSequence(0)),
            alignedHistory,
            []);
        var configuration = new EffectiveConfigurationSnapshot(
            new ConfigurationVersion(1),
            new ContentHash("sha256:configuration"),
            [],
            []);
        var evidence = new ContextAssemblyEvidence(agent, identity, historyView, authorization, configuration);
        return new ContextAssemblyRequest(
            agentId,
            sessionId,
            branchId,
            runId,
            turnId,
            new ModelRequestId(Guid.NewGuid()),
            TestFactory.Model(),
            [],
            evidence,
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
    }

    private sealed class ReferenceContributor: IContextContributor
    {
        public ValueTask<ContextContribution> ContributeAsync(
            ContextContributionRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ContextContribution([CreateReferenceCandidate()], []));

        private static ContextCandidate CreateReferenceCandidate() => new(
            new ContextSourceReference(
                new ContextSourceNamespace("agentkit.context"),
                new ContextSourceKey("reference"),
                new ContextSourceVersion("1")),
            ContextCandidateKind.ReferenceData,
            ContextTrust.Workspace,
            priority: 0,
            ContextScope.ModelRequest,
            new ContextCostEstimate(8, 2),
            ContextFreshness.Pinned,
            ContextEvaluationFrequency.OncePerModelRequest,
            mandatory: false,
            [],
            ExtensionData.Empty);
    }

    private sealed class UntrustedInstructionContributor: IContextContributor
    {
        public ValueTask<ContextContribution> ContributeAsync(
            ContextContributionRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ContextContribution([UntrustedInstructionCandidate()], []));

        private static ContextCandidate UntrustedInstructionCandidate() => new(
            new ContextSourceReference(
                new ContextSourceNamespace("agentkit.context"),
                new ContextSourceKey("bad"),
                new ContextSourceVersion("1")),
            ContextCandidateKind.Instruction,
            ContextTrust.ToolData,
            priority: 0,
            ContextScope.ModelRequest,
            new ContextCostEstimate(8, 2),
            ContextFreshness.Pinned,
            ContextEvaluationFrequency.OncePerModelRequest,
            mandatory: false,
            [],
            ExtensionData.Empty);
    }

    private sealed class CountingContributor: IContextContributor
    {
        internal static int InvocationCount { get; private set; }

        internal static void Reset() => InvocationCount = 0;

        public ValueTask<ContextContribution> ContributeAsync(
            ContextContributionRequest request,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return ValueTask.FromResult(new ContextContribution([], []));
        }
    }
}
