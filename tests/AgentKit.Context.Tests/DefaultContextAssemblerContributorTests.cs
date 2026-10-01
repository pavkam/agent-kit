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

    [Fact]
    public async Task AssembleAsync_WhenTheRequestCarriesAHookContext_ForwardsItToEveryContributor()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentContext();
        _ = services.AddContextContributor<HookCapturingContributor>(
            AgentContextComponentDefaults.AssemblerKey,
            new ContextContributorRegistration(
                new ContextSourceKey("hooks"),
                order: 0,
                ContextEvaluationFrequency.OncePerModelRequest,
                required: false));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var assembler = scope.ServiceProvider.GetRequiredService<IContextAssembler>();
        var request = CreateEvidenceRequest([TestFactory.UserMessage()]);
        var hooks = await HookDispatchContextFixtures.CreateAsync(AgentHookPoints.BeforeRetrieval, request.Evidence.Authorization.Scope.Correlation);

        HookCapturingContributor.Seen.Clear();
        _ = await assembler.AssembleAsync(request with { Hooks = hooks }, TestContext.Current.CancellationToken);
        _ = await assembler.AssembleAsync(request, TestContext.Current.CancellationToken);

        HookCapturingContributor.Seen.Count.ShouldBe(2);
        HookCapturingContributor.Seen[0].ShouldBeSameAs(hooks);
        HookCapturingContributor.Seen[1].ShouldBeNull();
    }

    private static ContextAssemblyRequest CreateEvidenceRequest(ImmutableArray<AgentMessage> history) =>
        TestFactory.AssemblyRequest(history);

    private sealed class HookCapturingContributor: IContextContributor
    {
        internal static List<HookDispatchContext?> Seen { get; } = [];

        public ValueTask<ContextContribution> ContributeAsync(
            ContextContributionRequest request,
            CancellationToken cancellationToken = default)
        {
            Seen.Add(request.Hooks);
            return ValueTask.FromResult(new ContextContribution([], []));
        }
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
