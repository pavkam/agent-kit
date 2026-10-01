// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

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

        var result = await harness.Coordinator.ProposeAsync(proposal, TestContext.Current.CancellationToken);

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

        var first = await harness.Coordinator.ProposeAsync(proposal, TestContext.Current.CancellationToken);
        var second = await harness.Coordinator.ProposeAsync(proposal, TestContext.Current.CancellationToken);

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

        var result = await harness.Coordinator.ProposeAsync(proposal, TestContext.Current.CancellationToken);

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

        var result = await harness.Coordinator.ProposeAsync(proposal, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(MemoryProposalOutcome.PolicyDenied);
        result.Denial!.Code.ShouldBe("no-secrets");
    }

    [Fact]
    public async Task ProposeAsync_WhenClassificationExceedsTheProfileCeiling_DeniesBeforeAnyPolicyOrWrite()
    {
        using var harness = MemoryHarness.Create(profile: configured => configured.MaximumClassification = DataClassification.Internal);
        var proposal = MemoryTestData.Proposal(MemoryTestData.NewOwner(), classification: DataClassification.Restricted);

        var result = await harness.Coordinator.ProposeAsync(proposal, TestContext.Current.CancellationToken);

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

        var result = await harness.Coordinator.ProposeAsync(proposal, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(MemoryProposalOutcome.Rejected);
        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
        harness.Grants.ConsumedCount.ShouldBe(0);
    }

    [Fact]
    public async Task ProposeAsync_WhenTheAuthorityFails_RejectsAsUnavailable()
    {
        using var harness = MemoryHarness.Create();
        harness.Authority.Throw = true;

        var result = await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(MemoryTestData.NewOwner()), TestContext.Current.CancellationToken);

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

        var result = await harness.Coordinator.ProposeAsync(proposal, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(MemoryProposalOutcome.Rejected);
        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Unavailable);
    }

    [Fact]
    public async Task ProposeAsync_WhenProposalIsNull_ThrowsArgumentNullException()
    {
        using var harness = MemoryHarness.Create();

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await harness.Coordinator.ProposeAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("proposal");
    }

    [Fact]
    public async Task ProposeAsync_WhenAlreadyCancelled_PropagatesCancellation()
    {
        using var harness = MemoryHarness.Create();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(MemoryTestData.NewOwner()), source.Token));
    }

    [Fact]
    public async Task CorrectAsync_WhenVersionMatches_SupersedesTheRecordAndCreatesTheReplacement()
    {
        using var harness = WithSink();
        var sink = harness.Provider.GetRequiredService<CollectingSink>();
        var owner = MemoryTestData.NewOwner();
        var accepted = (await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), TestContext.Current.CancellationToken)).Record!;

        var result = await harness.Coordinator.CorrectAsync(
            new MemoryCorrectionRequest(
                owner.Context, accepted.Id, accepted.Version, new MemoryId(Guid.NewGuid()), new MemoryContent("Corrected text."),
                new Provenance("user", owner.RunId, owner.SessionId), new IdempotencyKey("correct-1")),
            TestContext.Current.CancellationToken);

        result.IsTransitioned.ShouldBeTrue();
        result.Record!.State.ShouldBe(MemoryLifecycleState.Corrected);
        result.Replacement!.State.ShouldBe(MemoryLifecycleState.Active);
        result.Replacement.Content.Text.ShouldBe("Corrected text.");
        sink.Events.Select(static memoryEvent => memoryEvent.Kind).ShouldContain(MemoryEventKind.MemoryCorrected);
    }

    [Fact]
    public async Task CorrectAsync_WhenVersionIsStale_RejectsWithVersionConflict()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var accepted = (await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), TestContext.Current.CancellationToken)).Record!;

        var result = await harness.Coordinator.CorrectAsync(
            new MemoryCorrectionRequest(
                owner.Context, accepted.Id, new VersionToken("stale"), new MemoryId(Guid.NewGuid()), new MemoryContent("Corrected text."),
                new Provenance("user", owner.RunId, owner.SessionId), new IdempotencyKey("correct-2")),
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
                new Provenance("user", owner.RunId, owner.SessionId), new IdempotencyKey("correct-3")),
            TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
    }

    [Fact]
    public async Task DeleteAsync_WhenTombstoning_LogicallyDeletesAndPublishesOnce()
    {
        using var harness = WithSink();
        var sink = harness.Provider.GetRequiredService<CollectingSink>();
        var owner = MemoryTestData.NewOwner();
        var accepted = (await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), TestContext.Current.CancellationToken)).Record!;

        var result = await harness.Coordinator.DeleteAsync(
            new MemoryDeleteCommand(owner.Context, accepted.Id, accepted.Version, MemoryDeleteMode.Tombstone, new IdempotencyKey("delete-1")),
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
        var accepted = (await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), TestContext.Current.CancellationToken)).Record!;

        var result = await harness.Coordinator.DeleteAsync(
            new MemoryDeleteCommand(owner.Context, accepted.Id, accepted.Version, MemoryDeleteMode.Purge, new IdempotencyKey("delete-2")),
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
            new MemoryDeleteCommand(owner.Context, new MemoryId(Guid.NewGuid()), null, MemoryDeleteMode.Tombstone, new IdempotencyKey("delete-3")),
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
        var accepted = (await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner), TestContext.Current.CancellationToken)).Record!;

        var result = await harness.Coordinator.DeleteAsync(
            new MemoryDeleteCommand(stranger.Context, accepted.Id, null, MemoryDeleteMode.Purge, new IdempotencyKey("delete-4")),
            TestContext.Current.CancellationToken);

        result.IsDeleted.ShouldBeFalse();
        var read = await harness.Store.ReadAsync(
            new MemoryStoreRequestFactory(harness.Grants, harness.Store.Descriptor.SecurityAudience).Read(accepted.Id, owner.Authorization),
            TestContext.Current.CancellationToken);
        read.IsFound.ShouldBeTrue();
    }
}
