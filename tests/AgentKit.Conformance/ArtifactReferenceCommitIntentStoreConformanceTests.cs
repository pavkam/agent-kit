// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines mandatory tenant-partitioning, idempotency, conditional-transition, race, listing, validation, and cancellation cases for <see cref="IArtifactReferenceCommitIntentStore"/>.</summary>
/// <typeparam name="TFixture">The implementation fixture composed independently for each case.</typeparam>
public abstract class ArtifactReferenceCommitIntentStoreConformanceTests<TFixture>
    where TFixture : IArtifactReferenceCommitIntentStoreConformanceFixture
{
    private static readonly DateTimeOffset _now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly TenantId _tenant = new("tenant");
    private static readonly TenantId _otherTenant = new("other");
    private static readonly ArtifactReferenceCommitIntentId _id = new(Guid.Parse("a0000000-0000-0000-0000-0000000000a1"));

    /// <summary>Creates a fresh fixture with isolated store state.</summary>
    /// <returns>The fixture owned by one test case.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies RecordAsync: when first recorded equivalent or conflicting, applies replays or rejects.</summary>
    [Fact]
    public async Task RecordAsync_WhenFirstRecordedEquivalentOrConflicting_AppliesReplaysOrRejects()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);

        var first = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);
        var replay = await store.RecordAsync(Intent(pinFor: TimeSpan.FromDays(9)), TestContext.Current.CancellationToken);
        var conflict = await store.RecordAsync(Intent(id: Id("a2")), TestContext.Current.CancellationToken);

        first.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Applied);
        replay.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Replayed);
        replay.Intent.ShouldBe(first.Intent);
        conflict.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Conflict);
        conflict.Intent!.Id.ShouldBe(_id);
    }

    /// <summary>Verifies GetAsync: when another tenant asks, is indistinguishable from absent.</summary>
    [Fact]
    public async Task GetAsync_WhenAnotherTenantAsks_IsIndistinguishableFromAbsent()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);

        var owner = await store.GetAsync(_tenant, PreparationOf(1), TestContext.Current.CancellationToken);
        var foreign = await store.GetAsync(_otherTenant, PreparationOf(1), TestContext.Current.CancellationToken);
        var unknown = await store.GetAsync(_tenant, PreparationOf(9), TestContext.Current.CancellationToken);

        owner.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Replayed);
        foreign.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.NotFound);
        unknown.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.NotFound);
    }

    /// <summary>Verifies RecordAsync: when tenants share a preparation identity, partitions them.</summary>
    [Fact]
    public async Task RecordAsync_WhenTenantsShareAPreparationIdentity_PartitionsThem()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);

        var first = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);
        var second = await store.RecordAsync(Intent(tenant: _otherTenant, id: Id("a3")), TestContext.Current.CancellationToken);

        first.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Applied);
        second.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Applied);
    }

    /// <summary>Verifies TransitionAsync: when pending moves to a legal state, applies and stamps the transition.</summary>
    [Theory]
    [InlineData(ArtifactReferenceCommitState.Committed)]
    [InlineData(ArtifactReferenceCommitState.Fenced)]
    public async Task TransitionAsync_WhenPendingMovesToALegalState_AppliesAndStampsTheTransition(ArtifactReferenceCommitState next)
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);

        var result = await store.TransitionAsync(_tenant, PreparationOf(1), ArtifactReferenceCommitState.Pending, next, _now.AddHours(2), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Applied);
        result.Intent!.State.ShouldBe(next);
        result.Intent.UpdatedAt.ShouldBe(_now.AddHours(2));
        result.Intent.RecordedAt.ShouldBe(_now);
    }

    /// <summary>Verifies TransitionAsync: when already in the target state, replays without changing it.</summary>
    [Fact]
    public async Task TransitionAsync_WhenAlreadyInTheTargetState_ReplaysWithoutChangingIt()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);
        var first = await store.TransitionAsync(_tenant, PreparationOf(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, _now.AddHours(1), TestContext.Current.CancellationToken);

        var replay = await store.TransitionAsync(_tenant, PreparationOf(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, _now.AddHours(9), TestContext.Current.CancellationToken);

        replay.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Replayed);
        replay.Intent.ShouldBe(first.Intent);
    }

    /// <summary>Verifies TransitionAsync: when another actor won the race, reports state changed and keeps the winner.</summary>
    [Fact]
    public async Task TransitionAsync_WhenAnotherActorWonTheRace_ReportsStateChangedAndKeepsTheWinner()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);
        _ = await store.TransitionAsync(_tenant, PreparationOf(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, _now, TestContext.Current.CancellationToken);

        var late = await store.TransitionAsync(_tenant, PreparationOf(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Committed, _now, TestContext.Current.CancellationToken);

        late.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.StateChanged);
        late.Intent!.State.ShouldBe(ArtifactReferenceCommitState.Fenced);
    }

    /// <summary>Verifies TransitionAsync: when the transition is illegal, reports conflict.</summary>
    [Theory]
    [InlineData(ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Collected)]
    [InlineData(ArtifactReferenceCommitState.Fenced, ArtifactReferenceCommitState.Committed)]
    [InlineData(ArtifactReferenceCommitState.Committed, ArtifactReferenceCommitState.Fenced)]
    public async Task TransitionAsync_WhenTheTransitionIsIllegal_ReportsConflict(ArtifactReferenceCommitState from, ArtifactReferenceCommitState to)
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(state: from), TestContext.Current.CancellationToken);

        var result = await store.TransitionAsync(_tenant, PreparationOf(1), from, to, _now, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.Conflict);
        result.Intent!.State.ShouldBe(from);
    }

    /// <summary>Verifies TransitionAsync: when the intent is unknown or foreign, reports not found.</summary>
    [Fact]
    public async Task TransitionAsync_WhenTheIntentIsUnknownOrForeign_ReportsNotFound()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);

        var unknown = await store.TransitionAsync(_tenant, PreparationOf(9), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, _now, TestContext.Current.CancellationToken);
        var foreign = await store.TransitionAsync(_otherTenant, PreparationOf(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, _now, TestContext.Current.CancellationToken);

        unknown.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.NotFound);
        foreign.Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.NotFound);
    }

    /// <summary>Verifies TransitionAsync: when the transition instant precedes recording, never moves updated at backwards.</summary>
    [Fact]
    public async Task TransitionAsync_WhenTheTransitionInstantPrecedesRecording_NeverMovesUpdatedAtBackwards()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);

        var result = await store.TransitionAsync(_tenant, PreparationOf(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, _now.AddDays(-1), TestContext.Current.CancellationToken);

        result.Intent!.UpdatedAt.ShouldBe(_now);
    }

    /// <summary>Verifies TransitionAsync: when a fence and a commit race concurrently, exactly one wins.</summary>
    [Fact]
    public async Task TransitionAsync_WhenAFenceAndACommitRaceConcurrently_ExactlyOneWins()
    {
        for (var attempt = 0; attempt < 25; attempt++)
        {
            await using var fixture = CreateFixture();
            var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
            _ = await store.RecordAsync(Intent(), TestContext.Current.CancellationToken);

            var results = await Task.WhenAll(
                Task.Run(async () => await store.TransitionAsync(_tenant, PreparationOf(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Committed, _now, TestContext.Current.CancellationToken)),
                Task.Run(async () => await store.TransitionAsync(_tenant, PreparationOf(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, _now, TestContext.Current.CancellationToken)));

            results.Count(static result => result.Outcome == ArtifactReferenceCommitIntentOutcome.Applied).ShouldBe(1);
            results.Count(static result => result.Outcome == ArtifactReferenceCommitIntentOutcome.StateChanged).ShouldBe(1);
        }
    }

    /// <summary>Verifies ListPendingAsync: when intents differ, returns only older pending ones for the tenant oldest first bounded.</summary>
    [Fact]
    public async Task ListPendingAsync_WhenIntentsDiffer_ReturnsOnlyOlderPendingOnesForTheTenantOldestFirstBounded()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(preparation: 1, recorded: _now.AddMinutes(3)), TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(preparation: 2, recorded: _now.AddMinutes(1), id: Id("b2")), TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(preparation: 3, recorded: _now.AddMinutes(2), id: Id("b3")), TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(preparation: 4, recorded: _now.AddMinutes(0), id: Id("b4"), state: ArtifactReferenceCommitState.Fenced), TestContext.Current.CancellationToken);
        _ = await store.RecordAsync(Intent(preparation: 5, recorded: _now, id: Id("b5"), tenant: _otherTenant), TestContext.Current.CancellationToken);

        var all = await store.ListPendingAsync(_tenant, _now.AddMinutes(10), 10, TestContext.Current.CancellationToken);
        var before = await store.ListPendingAsync(_tenant, _now.AddMinutes(2), 10, TestContext.Current.CancellationToken);
        var bounded = await store.ListPendingAsync(_tenant, _now.AddMinutes(10), 1, TestContext.Current.CancellationToken);

        all.Select(static intent => intent.PreparationId).ShouldBe([PreparationOf(2), PreparationOf(3), PreparationOf(1)]);
        before.Select(static intent => intent.PreparationId).ShouldBe([PreparationOf(2)]);
        bounded.Select(static intent => intent.PreparationId).ShouldBe([PreparationOf(2)]);
    }

    /// <summary>Verifies Operations: when arguments are invalid, throw naming them before any effect.</summary>
    [Fact]
    public async Task Operations_WhenArgumentsAreInvalid_ThrowNamingThemBeforeAnyEffect()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.RecordAsync(null!))).ParamName.ShouldBe("intent");
        (await Should.ThrowAsync<ArgumentException>(async () => await store.GetAsync(default, PreparationOf(1)))).ParamName.ShouldBe("tenantId");
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await store.GetAsync(_tenant, default))).ParamName.ShouldBe("preparationId");
        (await Should.ThrowAsync<ArgumentException>(async () => await store.TransitionAsync(default, PreparationOf(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, _now))).ParamName.ShouldBe("tenantId");
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await store.TransitionAsync(_tenant, default, ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, _now))).ParamName.ShouldBe("preparationId");
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await store.TransitionAsync(_tenant, PreparationOf(1), (ArtifactReferenceCommitState) 99, ArtifactReferenceCommitState.Fenced, _now))).ParamName.ShouldBe("expected");
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await store.TransitionAsync(_tenant, PreparationOf(1), ArtifactReferenceCommitState.Pending, (ArtifactReferenceCommitState) 99, _now))).ParamName.ShouldBe("nextState");
        (await Should.ThrowAsync<ArgumentException>(async () => await store.ListPendingAsync(default, _now, 1))).ParamName.ShouldBe("tenantId");
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await store.ListPendingAsync(_tenant, _now, 0))).ParamName.ShouldBe("limit");
    }

    /// <summary>Verifies Operations: when cancelled, throw before any effect.</summary>
    [Fact]
    public async Task Operations_WhenCancelled_ThrowBeforeAnyEffect()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.RecordAsync(Intent(), cancelled.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.GetAsync(_tenant, PreparationOf(1), cancelled.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.TransitionAsync(_tenant, PreparationOf(1), ArtifactReferenceCommitState.Pending, ArtifactReferenceCommitState.Fenced, _now, cancelled.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.ListPendingAsync(_tenant, _now, 1, cancelled.Token));
        (await store.GetAsync(_tenant, PreparationOf(1), TestContext.Current.CancellationToken)).Outcome.ShouldBe(ArtifactReferenceCommitIntentOutcome.NotFound);
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
        var at = recorded ?? _now;
        return new ArtifactReferenceCommitIntent(
            identity, tenant ?? _tenant, PreparationOf(preparation), new ArtifactId(Guid.Parse("b0000000-0000-0000-0000-000000000001")), new ArtifactVersion("1"), new ArtifactOwnerId("owner"),
            new ArtifactPin(identity, at, at + (pinFor ?? TimeSpan.FromHours(1))), state, at, at);
    }

    private static ArtifactPreparationId PreparationOf(int n) => new(new Guid(n, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]));
}
