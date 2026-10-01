// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

using AgentKit.Hooks;

/// <summary>Verifies the coordinator's propose, correct, and delete behavior and its fail-closed guards.</summary>
public sealed class DefaultMemoryCoordinatorTests
{
    private sealed class CollectingSink: IMemoryEventSink
    {
        internal List<MemoryEvent> Events { get; } = [];

        public ValueTask PublishAsync(MemoryEvent memoryEvent, CancellationToken cancellationToken = default)
        {
            lock (Events)
            {
                Events.Add(memoryEvent);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class DenyPolicy: IMemoryPolicy
    {
        public ValueTask<MemoryPolicyDecision> EvaluateAsync(MemoryProposal proposal, MemoryPolicyContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<MemoryPolicyDecision>(new MemoryPolicyDenied(new ComponentId("tests.deny"), "no-secrets", "Secrets are not retained."));
    }

    private static MemoryHarness WithSink() => MemoryHarness.Create(arrange: services =>
    {
        _ = services.AddMemoryEventSink<CollectingSink>(new MemoryEventSinkRegistration(new ComponentId("tests.sink"), 0, MemoryEventDelivery.Required, ServiceLifetime.Singleton));
    });

    [Fact]
    public async Task ProposeAsync_WhenPolicyAllows_PersistsAnActiveRecordAndPublishesAcceptance()
    {
        using var harness = WithSink();
        var sink = harness.Provider.GetRequiredService<CollectingSink>();
        var owner = MemoryTestData.NewOwner();
        var proposal = MemoryTestData.Proposal(owner);

        var result = await harness.Coordinator.ProposeAsync(proposal, hooks: null, TestContext.Current.CancellationToken);

        result.IsAccepted.ShouldBeTrue();
        result.Record!.State.ShouldBe(MemoryLifecycleState.Active);
        result.Record.Id.ShouldBe(proposal.Id);
        result.Record.TenantId.ShouldBe(owner.Identity.TenantId);
        sink.Events.Select(static memoryEvent => memoryEvent.Kind).ShouldBe([MemoryEventKind.ProposalAccepted]);
        harness.Grants.ConsumedCount.ShouldBe(1);
    }

    [Fact]
    public async Task ProposeAsync_WhenReplayedWithTheSameProposal_IsIdempotentAndPublishesOnce()
    {
        using var harness = WithSink();
        var sink = harness.Provider.GetRequiredService<CollectingSink>();
        var proposal = MemoryTestData.Proposal(MemoryTestData.NewOwner());

        var first = await harness.Coordinator.ProposeAsync(proposal, hooks: null, TestContext.Current.CancellationToken);
        var second = await harness.Coordinator.ProposeAsync(proposal, hooks: null, TestContext.Current.CancellationToken);

        first.Replayed.ShouldBeFalse();
        second.IsAccepted.ShouldBeTrue();
        second.Replayed.ShouldBeTrue();
        sink.Events.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ProposeAsync_WhenNoPolicyExplicitlyAllows_DeniesFailClosedAndWritesNothing()
    {
        using var harness = MemoryHarness.Create(
            arrange: services =>
            {
                _ = services.AddMemoryEventSink<CollectingSink>(new MemoryEventSinkRegistration(new ComponentId("tests.sink"), 0, MemoryEventDelivery.Required, ServiceLifetime.Singleton));
            },
            allowPolicy: false);
        var sink = harness.Provider.GetRequiredService<CollectingSink>();
        var proposal = MemoryTestData.Proposal(MemoryTestData.NewOwner());

        var result = await harness.Coordinator.ProposeAsync(proposal, hooks: null, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(MemoryProposalOutcome.PolicyDenied);
        result.Denial!.Code.ShouldBe("no-explicit-allow");
        sink.Events.Select(static memoryEvent => memoryEvent.Kind).ShouldBe([MemoryEventKind.ProposalDenied]);
        harness.Grants.ConsumedCount.ShouldBe(0);
    }

    [Fact]
    public async Task ProposeAsync_WhenAPolicyDenies_ReturnsThatDenialEvenIfAnotherAllows()
    {
        using var harness = MemoryHarness.Create(arrange: services =>
            services.AddMemoryPolicy<DenyPolicy>(new MemoryPolicyRegistration(MemoryHarness.PolicyProfile, new ComponentId("tests.deny"), -1, ServiceLifetime.Singleton)));
        var proposal = MemoryTestData.Proposal(MemoryTestData.NewOwner());

        var result = await harness.Coordinator.ProposeAsync(proposal, hooks: null, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(MemoryProposalOutcome.PolicyDenied);
        result.Denial!.Code.ShouldBe("no-secrets");
    }

    [Fact]
    public async Task ProposeAsync_WhenClassificationExceedsTheProfileCeiling_DeniesBeforeAnyPolicyOrWrite()
    {
        using var harness = MemoryHarness.Create(profile: configured => configured.MaximumClassification = DataClassification.Internal);
        var proposal = MemoryTestData.Proposal(MemoryTestData.NewOwner(), classification: DataClassification.Restricted);

        var result = await harness.Coordinator.ProposeAsync(proposal, hooks: null, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(MemoryProposalOutcome.PolicyDenied);
        result.Denial!.Code.ShouldBe("classification-exceeded");
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProposeAsync_WhenTheAuthorityDenies_RejectsWithoutWriting()
    {
        using var harness = MemoryHarness.Create();
        harness.Authority.Deny = true;
        var proposal = MemoryTestData.Proposal(MemoryTestData.NewOwner());

        var result = await harness.Coordinator.ProposeAsync(proposal, hooks: null, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(MemoryProposalOutcome.Rejected);
        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
        harness.Grants.ConsumedCount.ShouldBe(0);
    }

    [Fact]
    public async Task ProposeAsync_WhenTheAuthorityFails_RejectsAsUnavailable()
    {
        using var harness = MemoryHarness.Create();
        harness.Authority.Throw = true;

        var result = await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(MemoryTestData.NewOwner()), hooks: null, TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Unavailable);
    }

    [Fact]
    public async Task ProposeAsync_WhenTheProfileIsUnknown_RejectsAsUnavailableWithoutThrowing()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var context = new MemoryOperationContext(
            owner.AgentId, owner.SessionId, owner.Identity, owner.Context.Correlation, owner.Authorization, new MemoryProfileKey("missing"), MemoryTestData.ProfileVersion);
        var proposal = MemoryTestData.Proposal(owner with { Context = context });

        var result = await harness.Coordinator.ProposeAsync(proposal, hooks: null, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(MemoryProposalOutcome.Rejected);
        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Unavailable);
    }

    [Fact]
    public async Task ProposeAsync_WhenProposalIsNull_ThrowsArgumentNullException()
    {
        using var harness = MemoryHarness.Create();

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await harness.Coordinator.ProposeAsync(null!, hooks: null, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("proposal");
    }

    [Fact]
    public async Task ProposeAsync_WhenAlreadyCancelled_PropagatesCancellation()
    {
        using var harness = MemoryHarness.Create();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(MemoryTestData.NewOwner()), hooks: null, source.Token));
    }

    [Fact]
    public async Task CorrectAsync_WhenVersionMatches_SupersedesTheRecordAndCreatesTheReplacement()
    {
        using var harness = WithSink();
        var sink = harness.Provider.GetRequiredService<CollectingSink>();
        var owner = MemoryTestData.NewOwner();
        var accepted = (await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), hooks: null, TestContext.Current.CancellationToken)).Record!;

        var result = await harness.Coordinator.CorrectAsync(
            new MemoryCorrectionRequest(
                owner.Context, accepted.Id, accepted.Version, new MemoryId(Guid.NewGuid()), new MemoryContent("Corrected text."),
                new Provenance("user", owner.RunId, owner.SessionId), new IdempotencyKey("correct-1")), hooks: null,
            TestContext.Current.CancellationToken);

        result.IsTransitioned.ShouldBeTrue();
        result.Record!.State.ShouldBe(MemoryLifecycleState.Corrected);
        result.Replacement!.State.ShouldBe(MemoryLifecycleState.Active);
        result.Replacement.Content.Text.ShouldBe("Corrected text.");
        sink.Events.Select(static memoryEvent => memoryEvent.Kind).ShouldContain(MemoryEventKind.MemoryCorrected);
    }

    private sealed class ScriptedProposalHook(string name, List<string> log, Action<BeforeMemoryProposalEventArgs>? act = null): IBeforeMemoryProposalHook
    {
        public ValueTask InvokeAsync(BeforeMemoryProposalEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default)
        {
            log.Add(name);
            act?.Invoke(args);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ScriptedWriteHook(string name, List<string> log, Action<BeforeMemoryWriteEventArgs>? act = null): IBeforeMemoryWriteHook
    {
        public ValueTask InvokeAsync(BeforeMemoryWriteEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default)
        {
            log.Add(name);
            act?.Invoke(args);
            return ValueTask.CompletedTask;
        }
    }

    private static MemoryHarness WithHooks() => MemoryHarness.Create(arrange: services => _ = services.AddAgentHooks());

    [Fact]
    public async Task ProposeAsync_WhenAProposalHookVetoes_DeniesBeforePolicyGrantsAndStoreWrite()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        var log = new List<string>();
        var proposal = MemoryTestData.Proposal(owner);
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("veto", AgentHookPointDefinitions.BeforeMemoryProposalRegistration), new ScriptedProposalHook(
                "veto", log, args => args.Veto = new MemoryHookVeto("tenant-policy", "Tenant policy forbids this memory."))));

        var result = await harness.Coordinator.ProposeAsync(proposal, MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(MemoryProposalOutcome.PolicyDenied);
        result.Denial!.Code.ShouldBe("tenant-policy");
        result.Denial.SafeMessage.ShouldBe("Tenant policy forbids this memory.");
        result.Denial.PolicyId.ShouldBe(new ComponentId("agentkit.memory.hook"));
        harness.Authority.Requests.ShouldBeEmpty();
        harness.Grants.ConsumedCount.ShouldBe(0);
        log.ShouldBe(["veto"]);
    }

    [Fact]
    public async Task ProposeAsync_WhenAWriteHookVetoes_DeniesAfterPolicyAllowedAndBeforeAnyGrantOrStoreWrite()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        var log = new List<string>();
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("proposal", AgentHookPointDefinitions.BeforeMemoryProposalRegistration), new ScriptedProposalHook("proposal", log)),
            (MemoryHookScope.Register("write", AgentHookPointDefinitions.BeforeMemoryWriteRegistration), new ScriptedWriteHook(
                "write", log, args => args.Veto = new MemoryHookVeto("retention-hold", "A retention hold prevents this write."))));

        var result = await harness.Coordinator.ProposeAsync(
            MemoryTestData.Proposal(owner), MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(MemoryProposalOutcome.PolicyDenied);
        result.Denial!.Code.ShouldBe("retention-hold");
        harness.Authority.Requests.ShouldBeEmpty();
        log.ShouldBe(["proposal", "write"]);
    }

    [Fact]
    public async Task ProposeAsync_WhenHooksDoNotVeto_RunsProposalThenWriteHooksAndAccepts()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        var log = new List<string>();
        var proposal = MemoryTestData.Proposal(owner);
        DurableMemoryRecord? written = null;
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("write", AgentHookPointDefinitions.BeforeMemoryWriteRegistration), new ScriptedWriteHook("write", log, args => written = args.Record)),
            (MemoryHookScope.Register("proposal", AgentHookPointDefinitions.BeforeMemoryProposalRegistration), new ScriptedProposalHook(
                "proposal", log, args => args.Proposal.ShouldBeSameAs(proposal))));

        var result = await harness.Coordinator.ProposeAsync(proposal, MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        result.IsAccepted.ShouldBeTrue();
        log.ShouldBe(["proposal", "write"]);
        written!.Id.ShouldBe(proposal.Id);
        written.Content.ShouldBe(proposal.Content);
    }

    [Fact]
    public async Task ProposeAsync_WhenSeveralProposalHooksAreRegistered_RunsThemInDeterministicCatalogOrder()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        var log = new List<string>();
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("last", AgentHookPointDefinitions.BeforeMemoryProposalRegistration, HookOrder.Last), new ScriptedProposalHook("last", log)),
            (MemoryHookScope.Register("normal", AgentHookPointDefinitions.BeforeMemoryProposalRegistration), new ScriptedProposalHook("normal", log)),
            (MemoryHookScope.Register("first", AgentHookPointDefinitions.BeforeMemoryProposalRegistration, HookOrder.First), new ScriptedProposalHook("first", log)));

        var result = await harness.Coordinator.ProposeAsync(
            MemoryTestData.Proposal(owner), MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        result.IsAccepted.ShouldBeTrue();
        log.ShouldBe(["first", "normal", "last"]);
    }

    [Fact]
    public async Task ProposeAsync_WhenAProposalHookThrows_RejectsWithAFixedSafeFailureAndWritesNothing()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("boom", AgentHookPointDefinitions.BeforeMemoryProposalRegistration), new ScriptedProposalHook(
                "boom", [], _ => throw new InvalidOperationException("secret detail"))));

        var result = await harness.Coordinator.ProposeAsync(
            MemoryTestData.Proposal(owner), MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(MemoryProposalOutcome.Rejected);
        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
        result.Failure.SafeMessage.ShouldBe("A memory hook failed, so the operation was refused.");
        result.Failure.SafeMessage.ShouldNotContain("secret detail");
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProposeAsync_WhenAWriteHookThrows_RejectsAfterPolicyAndWritesNothing()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("boom", AgentHookPointDefinitions.BeforeMemoryWriteRegistration), new ScriptedWriteHook(
                "boom", [], _ => throw new InvalidOperationException("secret detail"))));

        var result = await harness.Coordinator.ProposeAsync(
            MemoryTestData.Proposal(owner), MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(MemoryProposalOutcome.Rejected);
        result.Failure!.SafeMessage.ShouldBe("A memory hook failed, so the operation was refused.");
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProposeAsync_WhenNoHookContextIsSupplied_RunsNoRegisteredHook()
    {
        using var harness = WithHooks();
        var log = new List<string>();
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("proposal", AgentHookPointDefinitions.BeforeMemoryProposalRegistration), new ScriptedProposalHook("proposal", log)));

        var result = await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(MemoryTestData.NewOwner()), hooks: null, TestContext.Current.CancellationToken);

        result.IsAccepted.ShouldBeTrue();
        log.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProposeAsync_WhenTheCapturedCatalogRegistersOnlyAnotherPoint_DispatchesNothing()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        var log = new List<string>();
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("write", AgentHookPointDefinitions.BeforeMemoryWriteRegistration), new ScriptedWriteHook("write", log)));

        var result = await harness.Coordinator.ProposeAsync(
            MemoryTestData.Proposal(owner), MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        result.IsAccepted.ShouldBeTrue();
        log.ShouldBe(["write"]);
    }

    [Fact]
    public async Task ProposeAsync_WhenNoDispatcherIsComposed_IgnoresTheSuppliedHookContext()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var log = new List<string>();
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("proposal", AgentHookPointDefinitions.BeforeMemoryProposalRegistration), new ScriptedProposalHook("proposal", log)));

        var result = await harness.Coordinator.ProposeAsync(
            MemoryTestData.Proposal(owner), MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        result.IsAccepted.ShouldBeTrue();
        log.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProposeAsync_WhenHooksRun_EachSeesItsOwnPointAndTheCallersCorrelationAndDeadline()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        BeforeMemoryProposalEventArgs? proposalArgs = null;
        BeforeMemoryWriteEventArgs? writeArgs = null;
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("proposal", AgentHookPointDefinitions.BeforeMemoryProposalRegistration), new ScriptedProposalHook("proposal", [], args => proposalArgs = args)),
            (MemoryHookScope.Register("write", AgentHookPointDefinitions.BeforeMemoryWriteRegistration), new ScriptedWriteHook("write", [], args => writeArgs = args)));
        var context = MemoryHookScope.Context(scope, harness, owner);

        _ = await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), context, TestContext.Current.CancellationToken);

        proposalArgs!.Point.ShouldBe(AgentHookPoints.BeforeMemoryProposal);
        writeArgs!.Point.ShouldBe(AgentHookPoints.BeforeMemoryWrite);
        proposalArgs.DispatchId.ShouldNotBe(writeArgs.DispatchId);
        proposalArgs.DispatchId.ShouldNotBe(context.Dispatch.DispatchId);
        proposalArgs.Correlation.ShouldBe(context.Dispatch.Correlation);
        writeArgs.Deadline.ShouldBe(context.Dispatch.Deadline);
        proposalArgs.AgentId.ShouldBe(owner.Context.AgentId);
        writeArgs.SessionId.ShouldBe(owner.Context.SessionId);
    }

    [Fact]
    public async Task CorrectAsync_WhenAProposalHookVetoes_DeniesWithoutTransitioningTheOriginal()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        var accepted = (await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), hooks: null, TestContext.Current.CancellationToken)).Record!;
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("veto", AgentHookPointDefinitions.BeforeMemoryProposalRegistration), new ScriptedProposalHook(
                "veto", [], args => args.Veto = new MemoryHookVeto("frozen", "Corrections are frozen."))));

        var result = await harness.Coordinator.CorrectAsync(
            new MemoryCorrectionRequest(
                owner.Context, accepted.Id, accepted.Version, new MemoryId(Guid.NewGuid()), new MemoryContent("Corrected text."),
                new Provenance("user", owner.RunId, owner.SessionId), new IdempotencyKey("correct-veto")),
            MemoryHookScope.Context(scope, harness, owner),
            TestContext.Current.CancellationToken);

        result.IsTransitioned.ShouldBeFalse();
        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
        result.Failure.SafeMessage.ShouldBe("Corrections are frozen.");
        harness.Authority.Requests.ShouldNotContain(request => request.Effect == SecurityEffect.Mutate);
    }

    [Fact]
    public async Task CorrectAsync_WhenAWriteHookFails_RejectsWithAFixedSafeFailure()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        var accepted = (await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), hooks: null, TestContext.Current.CancellationToken)).Record!;
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("boom", AgentHookPointDefinitions.BeforeMemoryWriteRegistration), new ScriptedWriteHook(
                "boom", [], _ => throw new InvalidOperationException("secret detail"))));

        var result = await harness.Coordinator.CorrectAsync(
            new MemoryCorrectionRequest(
                owner.Context, accepted.Id, accepted.Version, new MemoryId(Guid.NewGuid()), new MemoryContent("Corrected text."),
                new Provenance("user", owner.RunId, owner.SessionId), new IdempotencyKey("correct-boom")),
            MemoryHookScope.Context(scope, harness, owner),
            TestContext.Current.CancellationToken);

        result.IsTransitioned.ShouldBeFalse();
        result.Failure!.SafeMessage.ShouldBe("A memory hook failed, so the operation was refused.");
    }

    [Fact]
    public async Task DeleteAsync_WhenAHookContextIsSupplied_DispatchesNoHookPoint()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        var log = new List<string>();
        var accepted = (await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), hooks: null, TestContext.Current.CancellationToken)).Record!;
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("proposal", AgentHookPointDefinitions.BeforeMemoryProposalRegistration), new ScriptedProposalHook("proposal", log)),
            (MemoryHookScope.Register("write", AgentHookPointDefinitions.BeforeMemoryWriteRegistration), new ScriptedWriteHook("write", log)));

        var result = await harness.Coordinator.DeleteAsync(
            new MemoryDeleteCommand(owner.Context, accepted.Id, accepted.Version, MemoryDeleteMode.Tombstone, new IdempotencyKey("delete-hooks")),
            MemoryHookScope.Context(scope, harness, owner),
            TestContext.Current.CancellationToken);

        result.IsDeleted.ShouldBeTrue();
        log.ShouldBeEmpty();
    }

    private sealed class FailingRequiredSink: IMemoryEventSink
    {
        public ValueTask PublishAsync(MemoryEvent memoryEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The sink failed.");
    }

    private sealed class ThrowingRuntimeSelector: IMemoryProfileRuntimeSelector
    {
        public ValueTask<MemoryProfileRuntimeSelectionResult> SelectAsync(MemoryOperationContext context, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("secret detail");
    }

    [Fact]
    public async Task ProposeAsync_WhenCancelled_LogsTheCancelledEventAtInformation()
    {
        var logger = new RecordingLogger<DefaultMemoryCoordinator>();
        using var harness = MemoryHarness.Create(arrange: services => services.AddSingleton<ILogger<DefaultMemoryCoordinator>>(logger));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(MemoryTestData.NewOwner()), hooks: null, source.Token));

        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(32311);
        entry.Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public async Task ProposeAsync_WhenTheRuntimeSelectorFaults_LogsTheFaultedEventWithTheErrorTypeAndRethrows()
    {
        var logger = new RecordingLogger<DefaultMemoryCoordinator>();
        using var harness = MemoryHarness.Create(arrange: services =>
        {
            _ = services.AddSingleton<ILogger<DefaultMemoryCoordinator>>(logger);
            _ = services.ReplaceMemoryProfileRuntimeSelector<ThrowingRuntimeSelector>();
        });

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(MemoryTestData.NewOwner()), hooks: null, TestContext.Current.CancellationToken));

        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(32312);
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Message.ShouldContain(nameof(InvalidOperationException));
        entry.Message.ShouldNotContain("secret detail");
    }

    [Fact]
    public async Task ProposeAsync_WhenARequiredSinkCannotRecordTheCommittedWrite_LogsTheRequiredObservationMissingEvent()
    {
        var logger = new RecordingLogger<DefaultMemoryCoordinator>();
        using var harness = MemoryHarness.Create(arrange: services =>
        {
            _ = services.AddSingleton<ILogger<DefaultMemoryCoordinator>>(logger);
            _ = services.AddMemoryEventSink<FailingRequiredSink>(new MemoryEventSinkRegistration(new ComponentId("tests.required"), 0, MemoryEventDelivery.Required, ServiceLifetime.Singleton));
        });

        var result = await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(MemoryTestData.NewOwner()), hooks: null, TestContext.Current.CancellationToken);

        result.IsAccepted.ShouldBeTrue();
        logger.Snapshot().Select(static entry => entry.EventId.Id).ShouldBe([32313, 32310]);
        logger.Snapshot()[0].Level.ShouldBe(LogLevel.Error);
    }

    [Fact]
    public async Task CorrectAsync_WhenVersionIsStale_RejectsWithVersionConflict()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var accepted = (await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), hooks: null, TestContext.Current.CancellationToken)).Record!;

        var result = await harness.Coordinator.CorrectAsync(
            new MemoryCorrectionRequest(
                owner.Context, accepted.Id, new VersionToken("stale"), new MemoryId(Guid.NewGuid()), new MemoryContent("Corrected text."),
                new Provenance("user", owner.RunId, owner.SessionId), new IdempotencyKey("correct-2")), hooks: null,
            TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.VersionConflict);
    }

    [Fact]
    public async Task CorrectAsync_WhenPolicyDenies_RejectsWithoutChangingTheRecord()
    {
        using var harness = MemoryHarness.Create(arrange: services =>
            services.AddMemoryPolicy<DenyPolicy>(new MemoryPolicyRegistration(MemoryHarness.PolicyProfile, new ComponentId("tests.deny"), -1, ServiceLifetime.Singleton)));
        var owner = MemoryTestData.NewOwner();
        var stored = MemoryTestData.Record(owner);
        var write = new MemoryStoreRequestFactory(harness.Grants, harness.Store.Descriptor.SecurityAudience).Write(stored, owner.Authorization);
        _ = await harness.Store.WriteAsync(write, TestContext.Current.CancellationToken);

        var result = await harness.Coordinator.CorrectAsync(
            new MemoryCorrectionRequest(
                owner.Context, stored.Id, stored.Version, new MemoryId(Guid.NewGuid()), new MemoryContent("Corrected text."),
                new Provenance("user", owner.RunId, owner.SessionId), new IdempotencyKey("correct-3")), hooks: null,
            TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
    }

    [Fact]
    public async Task DeleteAsync_WhenTombstoning_LogicallyDeletesAndPublishesOnce()
    {
        using var harness = WithSink();
        var sink = harness.Provider.GetRequiredService<CollectingSink>();
        var owner = MemoryTestData.NewOwner();
        var accepted = (await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), hooks: null, TestContext.Current.CancellationToken)).Record!;

        var result = await harness.Coordinator.DeleteAsync(
            new MemoryDeleteCommand(owner.Context, accepted.Id, accepted.Version, MemoryDeleteMode.Tombstone, new IdempotencyKey("delete-1")), hooks: null,
            TestContext.Current.CancellationToken);

        result.IsDeleted.ShouldBeTrue();
        result.Receipt!.LogicallyDeleted.ShouldBeTrue();
        sink.Events.Count(static memoryEvent => memoryEvent.Kind == MemoryEventKind.MemoryDeleted).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteAsync_WhenPurging_PhysicallyRemovesTheBody()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var accepted = (await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), hooks: null, TestContext.Current.CancellationToken)).Record!;

        var result = await harness.Coordinator.DeleteAsync(
            new MemoryDeleteCommand(owner.Context, accepted.Id, accepted.Version, MemoryDeleteMode.Purge, new IdempotencyKey("delete-2")), hooks: null,
            TestContext.Current.CancellationToken);

        result.IsDeleted.ShouldBeTrue();
        result.Receipt!.PhysicallyPurged.ShouldBeTrue();
        var read = await harness.Store.ReadAsync(
            new MemoryStoreRequestFactory(harness.Grants, harness.Store.Descriptor.SecurityAudience).Read(accepted.Id, owner.Authorization),
            TestContext.Current.CancellationToken);
        read.IsFound.ShouldBeFalse();
        _ = read.Tombstone.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenTheRecordIsMissing_RejectsAsNotFound()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();

        var result = await harness.Coordinator.DeleteAsync(
            new MemoryDeleteCommand(owner.Context, new MemoryId(Guid.NewGuid()), null, MemoryDeleteMode.Tombstone, new IdempotencyKey("delete-3")), hooks: null,
            TestContext.Current.CancellationToken);

        result.IsDeleted.ShouldBeFalse();
        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_WhenAnotherTenantDeletes_IsRejectedAndTheRecordSurvives()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner("tenant-a");
        var stranger = MemoryTestData.NewOwner("tenant-b");
        var accepted = (await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), hooks: null, TestContext.Current.CancellationToken)).Record!;

        var result = await harness.Coordinator.DeleteAsync(
            new MemoryDeleteCommand(stranger.Context, accepted.Id, null, MemoryDeleteMode.Purge, new IdempotencyKey("delete-4")), hooks: null,
            TestContext.Current.CancellationToken);

        result.IsDeleted.ShouldBeFalse();
        var read = await harness.Store.ReadAsync(
            new MemoryStoreRequestFactory(harness.Grants, harness.Store.Descriptor.SecurityAudience).Read(accepted.Id, owner.Authorization),
            TestContext.Current.CancellationToken);
        read.IsFound.ShouldBeTrue();
    }
}
