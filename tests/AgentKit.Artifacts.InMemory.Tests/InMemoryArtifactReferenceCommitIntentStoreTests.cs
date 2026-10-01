// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

using static ArtifactTestSupport;

/// <summary>Verifies the in-memory intent store's tenant partitioning, idempotency, and conditional transitions.</summary>
public sealed class InMemoryArtifactReferenceCommitIntentStoreTests
{
    private static readonly ArtifactReferenceCommitIntentId _id = new(Guid.Parse("a0000000-0000-0000-0000-0000000000a1"));

    [Fact]
    public async Task RecordAsync_WhenFirstRecordedEquivalentOrConflicting_AppliesReplaysOrRejects()
    {
        var store = new InMemoryArtifactReferenceCommitIntentStore();

        var first = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);
        var replay = await store.RecordAsync(Intent(pinFor: TimeSpan.FromDays(9)), TestContext.Current.CancellationToken);
        var conflict = await store.RecordAsync(Intent(id: Id("a2")), TestContext.Current.CancellationToken);

        first.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Applied);
        replay.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Replayed);
        replay.Intent.ShouldBe(first.Intent);
        conflict.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Conflict);
        conflict.Intent!.Id.ShouldBe(_id);
    }

    [Fact]
    public async Task GetAsync_WhenAnotherTenantAsks_IsIndistinguishableFromAbsent()
    {
        var store = new InMemoryArtifactReferenceCommitIntentStore();
        _ = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);

        var owner = await store.GetAsync(Identity.TenantId, Preparation(1), TestContext.Current.CancellationToken);
        var foreign = await store.GetAsync(Other.TenantId, Preparation(1), TestContext.Current.CancellationToken);
        var unknown = await store.GetAsync(Identity.TenantId, Preparation(9), TestContext.Current.CancellationToken);

        owner.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Replayed);
        foreign.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.NotFound);
        unknown.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.NotFound);
    }

    [Fact]
    public async Task RecordAsync_WhenTenantsShareAPreparationIdentity_PartitionsThem()
    {
        var store = new InMemoryArtifactReferenceCommitIntentStore();

        var first = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);
        var second = await store.RecordAsync(Intent(tenant: Other.TenantId, id: Id("a3")), TestContext.Current.CancellationToken);

        first.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Applied);
        second.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Applied);
    }

    [Theory]
    [InlineData(ArtifactReferenceCommitState.Committed)]
    [InlineData(ArtifactReferenceCommitState.Fenced)]
    public async Task TransitionAsync_WhenPendingMovesToALegalState_AppliesAndStampsTheTransition(ArtifactReferenceCommitState next)
    {
        var store = new InMemoryArtifactReferenceCommitIntentStore();
        _ = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);

        var result = await store.TransitionAsync(Identity.TenantId, Preparation(1), ArtifactReferenceCommitState.Pending, next, Now.AddHours(2), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Applied);
        result.Intent!.State.ShouldBe(next);
        result.Intent.UpdatedAt.ShouldBe(Now.AddHours(2));
        result.Intent.RecordedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task TransitionAsync_WhenAlreadyInTheTargetState_ReplaysWithoutChangingIt()
    {
        var store = new InMemoryArtifactReferenceCommitIntentStore();
        _ = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);
        var first = await store.TransitionAsync(Identity.TenantId, Preparation(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, Now.AddHours(1), TestContext.Current.CancellationToken);

        var replay = await store.TransitionAsync(Identity.TenantId, Preparation(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, Now.AddHours(9), TestContext.Current.CancellationToken);

        replay.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Replayed);
        replay.Intent.ShouldBe(first.Intent);
    }

    [Fact]
    public async Task TransitionAsync_WhenAnotherActorWonTheRace_ReportsStateChangedAndKeepsTheWinner()
    {
        var store = new InMemoryArtifactReferenceCommitIntentStore();
        _ = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);
        _ = await store.TransitionAsync(Identity.TenantId, Preparation(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, Now, TestContext.Current.CancellationToken);

        var late = await store.TransitionAsync(Identity.TenantId, Preparation(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Committed, Now, TestContext.Current.CancellationToken);

        late.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.StateChanged);
        late.Intent!.State.ShouldBe(ArtifactReferenceCommitState.Fenced);
    }

    [Theory]
    [InlineData(ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Collected)]
    [InlineData(ArtifactReferenceCommitState.Fenced, ArtifactReferenceCommitState.Committed)]
    [InlineData(ArtifactReferenceCommitState.Committed, ArtifactReferenceCommitState.Fenced)]
    public async Task TransitionAsync_WhenTheTransitionIsIllegal_ReportsConflict(ArtifactReferenceCommitState from, ArtifactReferenceCommitState to)
    {
        var store = new InMemoryArtifactReferenceCommitIntentStore();
        _ = await store.RecordAsync(Intent(state: from), TestContext.Current.CancellationToken);

        var result = await store.TransitionAsync(Identity.TenantId, Preparation(1), from, to, Now, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Conflict);
        result.Intent!.State.ShouldBe(from);
    }

    [Fact]
    public async Task TransitionAsync_WhenTheIntentIsUnknownOrForeign_ReportsNotFound()
    {
        var store = new InMemoryArtifactReferenceCommitIntentStore();
        _ = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);

        var unknown = await store.TransitionAsync(Identity.TenantId, Preparation(9), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, Now, TestContext.Current.CancellationToken);
        var foreign = await store.TransitionAsync(Other.TenantId, Preparation(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, Now, TestContext.Current.CancellationToken);

        unknown.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.NotFound);
        foreign.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.NotFound);
    }

    [Fact]
    public async Task TransitionAsync_WhenTheTransitionInstantPrecedesRecording_NeverMovesUpdatedAtBackwards()
    {
        var store = new InMemoryArtifactReferenceCommitIntentStore();
        _ = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);

        var result = await store.TransitionAsync(Identity.TenantId, Preparation(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, Now.AddDays(-1), TestContext.Current.CancellationToken);

        result.Intent!.UpdatedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task TransitionAsync_WhenAFenceAndACommitRaceConcurrently_ExactlyOneWins()
    {
        for (var attempt = 0; attempt < 25; attempt++)
        {
            var store = new InMemoryArtifactReferenceCommitIntentStore();
            _ = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);

            var results = await Task.WhenAll(
                Task.Run(async () => await store.TransitionAsync(Identity.TenantId, Preparation(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Committed, Now, TestContext.Current.CancellationToken)),
                Task.Run(async () => await store.TransitionAsync(Identity.TenantId, Preparation(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, Now, TestContext.Current.CancellationToken)));

            results.Count(static result => result.Outcome == ArtifactReferenceCommitIntentOutcome.Applied).ShouldBe(1);
            results.Count(static result => result.Outcome == ArtifactReferenceCommitIntentOutcome.StateChanged).ShouldBe(1);
        }
    }

    [Fact]
    public async Task ListPendingAsync_WhenIntentsDiffer_ReturnsOnlyOlderPendingOnesForTheTenantOldestFirstBounded()
    {
        var store = new InMemoryArtifactReferenceCommitIntentStore();
        _ = await store.RecordAsync(Intent(preparation: 1, recorded: Now.AddMinutes(3)), TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(preparation: 2, recorded: Now.AddMinutes(1), id: Id("b2")), TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(preparation: 3, recorded: Now.AddMinutes(2), id: Id("b3")), TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(preparation: 4, recorded: Now.AddMinutes(0), id: Id("b4"), state: ArtifactReferenceCommitState.Fenced), TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(preparation: 5, recorded: Now, id: Id("b5"), tenant: Other.TenantId), TestContext.Current.CancellationToken);

        var all = await store.ListPendingAsync(Identity.TenantId, Now.AddMinutes(10), 10, TestContext.Current.CancellationToken);
        var before = await store.ListPendingAsync(Identity.TenantId, Now.AddMinutes(2), 10, TestContext.Current.CancellationToken);
        var bounded = await store.ListPendingAsync(Identity.TenantId, Now.AddMinutes(10), 1, TestContext.Current.CancellationToken);

        all.Select(static intent => intent.PreparationId).ShouldBe([Preparation(2), Preparation(3), Preparation(1)]);
        before.Select(static intent => intent.PreparationId).ShouldBe([Preparation(2)]);
        bounded.Select(static intent => intent.PreparationId).ShouldBe([Preparation(2)]);
    }

    [Fact]
    public async Task Operations_WhenArgumentsAreInvalid_ThrowNamingThemBeforeAnyEffect()
    {
        var store = new InMemoryArtifactReferenceCommitIntentStore();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.RecordAsync(null!))).ParamName.ShouldBe("intent");
        (await Should.ThrowAsync<ArgumentException>(async () => await store.GetAsync(default, Preparation(1)))).ParamName.ShouldBe("tenantId");
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await store.GetAsync(Identity.TenantId, default))).ParamName.ShouldBe("preparationId");
        (await Should.ThrowAsync<ArgumentException>(async () => await store.TransitionAsync(default, Preparation(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, Now))).ParamName.ShouldBe("tenantId");
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await store.TransitionAsync(Identity.TenantId, default, ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, Now))).ParamName.ShouldBe("preparationId");
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await store.TransitionAsync(Identity.TenantId, Preparation(1), (ArtifactReferenceCommitState) 99, ArtifactReferenceCommitState.Fenced, Now))).ParamName.ShouldBe("expected");
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await store.TransitionAsync(Identity.TenantId, Preparation(1), ArtifactReferenceCommitState.Pending, (ArtifactReferenceCommitState) 99, Now))).ParamName.ShouldBe("nextState");
        (await Should.ThrowAsync<ArgumentException>(async () => await store.ListPendingAsync(default, Now, 1))).ParamName.ShouldBe("tenantId");
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await store.ListPendingAsync(Identity.TenantId, Now, 0))).ParamName.ShouldBe("limit");
    }

    [Fact]
    public async Task Operations_WhenCancelled_ThrowBeforeAnyEffect()
    {
        var store = new InMemoryArtifactReferenceCommitIntentStore();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.RecordAsync(Intent(), cancelled.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.GetAsync(Identity.TenantId, Preparation(1), cancelled.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.TransitionAsync(Identity.TenantId, Preparation(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, Now, cancelled.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.ListPendingAsync(Identity.TenantId, Now, 1, cancelled.Token));
        (await store.GetAsync(Identity.TenantId, Preparation(1), TestContext.Current.CancellationToken)).Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.NotFound);
    }

    private static ArtifactReferenceCommitIntentId Id(string suffix) => new(Guid.Parse($"a0000000-0000-0000-0000-0000000000{suffix}"));

    private static ArtifactReferenceCommitIntent Intent(
        int preparation = 1,
        TimeSpan? pinFor = null,
        ArtifactReferenceCommitState state = ArtifactReferenceCommitState.Pending,
        DateTimeOffset? recorded = null,
        ArtifactReferenceCommitIntentId? id = null,
        TenantId? tenant = null)
    {
        var identity = id ?? _id;
        var at = recorded ?? Now;
        return new ArtifactReferenceCommitIntent(
            identity, tenant ?? Identity.TenantId, Preparation(preparation), Artifact(1), new ArtifactVersion("1"), new ArtifactOwnerId("owner"),
            new ArtifactPin(identity, at, at + (pinFor ?? TimeSpan.FromHours(1))), state, at, at);
    }
}
