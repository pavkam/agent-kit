// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

public sealed class DefaultGoalCoordinatorTests
{
    private sealed class CollectingSink: IGoalEventSink
    {
        internal ConcurrentQueue<GoalEvent> Seen { get; } = [];

        public ValueTask PublishAsync(GoalEvent goalEvent, CancellationToken cancellationToken = default)
        {
            Seen.Enqueue(goalEvent);
            return ValueTask.CompletedTask;
        }
    }

    private static GoalCreateCommand Create(DelegationHarness.RunContext run, string key = "create") =>
        new(GoalTestData.Goal(run.AgentId, run.SessionId, run.RunId), null, new IdempotencyKey(key), run.Authorization);

    [Fact]
    public async Task CreateAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        await using var harness = new DelegationHarness();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await harness.Goals.CreateAsync(null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task CreateAsync_WhenAuthorized_CreatesGoalAndReplayReturnsTheSameRecord()
    {
        await using var harness = new DelegationHarness();
        var run = DelegationHarness.NewRun();
        var command = Create(run);

        var first = await harness.Goals.CreateAsync(command, TestContext.Current.CancellationToken);
        var replay = await harness.Goals.CreateAsync(command, TestContext.Current.CancellationToken);

        var created = first.ShouldBeOfType<GoalCreated>();
        created.Replayed.ShouldBeFalse();
        replay.ShouldBeOfType<GoalCreated>().Replayed.ShouldBeTrue();
        created.Record.Goal.Status.ShouldBe(GoalStatus.Proposed);
    }

    [Fact]
    public async Task CreateAsync_WhenAuthorityDenies_RejectsAsDeniedWithoutCreatingAnything()
    {
        await using var harness = new DelegationHarness();
        harness.Authority.Deny = true;
        var run = DelegationHarness.NewRun();
        var command = Create(run);

        var result = await harness.Goals.CreateAsync(command, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<GoalCreateRejected>().Failure.Kind.ShouldBe(GoalStoreFailureKind.Denied);
        harness.Authority.Deny = false;
        (await harness.Goals.LoadAsync(new GoalLoadCommand(GoalTestData.Profile, command.Goal.Id, run.Authorization), TestContext.Current.CancellationToken))
            .ShouldBeOfType<GoalLoadRejected>().Failure.Kind.ShouldBe(GoalStoreFailureKind.NotFound);
    }

    [Fact]
    public async Task CreateAsync_WhenProfileIsNotPublished_RejectsAsUnavailable()
    {
        await using var harness = new DelegationHarness();
        var run = DelegationHarness.NewRun();

        var goal = GoalTestData.Goal(run.AgentId, run.SessionId, run.RunId);
        var unknown = new AgentGoal(
            goal.Id, null, goal.OwnerAgentId, goal.SessionId, goal.OriginatingRunId, new GoalProfileKey("unknown"), new GoalProfileVersion(1),
            goal.AgentDefinitionRevision, GoalStatus.Proposed, goal.Definition, goal.Budget, null, goal.Version, goal.CreatedAt, goal.Extensions);

        var result = await harness.Goals.CreateAsync(new GoalCreateCommand(unknown, null, new IdempotencyKey("u"), run.Authorization), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<GoalCreateRejected>().Failure.Kind.ShouldBe(GoalStoreFailureKind.Unavailable);
    }

    [Fact]
    public async Task CreateAsync_WhenCreatedFirstTime_PublishesOneCreatedEventAndNoneOnReplay()
    {
        var sink = new CollectingSink();
        await using var harness = new DelegationHarness(configure: services =>
        {
            _ = services.AddGoalEventSink<CollectingSink>(new GoalEventSinkRegistration(new ComponentId("s"), 1, GoalEventDelivery.Required, ServiceLifetime.Singleton));
            _ = services.AddSingleton(sink);
        });
        var run = DelegationHarness.NewRun();
        var command = Create(run);

        _ = await harness.Goals.CreateAsync(command, TestContext.Current.CancellationToken);
        _ = await harness.Goals.CreateAsync(command, TestContext.Current.CancellationToken);

        sink.Seen.Where(static seen => seen.Kind == GoalEventKind.GoalCreated).ShouldHaveSingleItem().GoalId.ShouldBe(command.Goal.Id);
    }

    [Fact]
    public async Task TransitionAsync_WhenTransitionIsValid_AppliesItAndPublishesTransitionedEvent()
    {
        var sink = new CollectingSink();
        await using var harness = new DelegationHarness(configure: services =>
        {
            _ = services.AddGoalEventSink<CollectingSink>(new GoalEventSinkRegistration(new ComponentId("s"), 1, GoalEventDelivery.Required, ServiceLifetime.Singleton));
            _ = services.AddSingleton(sink);
        });
        var run = DelegationHarness.NewRun();
        var created = (GoalCreated) await harness.Goals.CreateAsync(Create(run), TestContext.Current.CancellationToken);

        var moved = await harness.Goals.TransitionAsync(
            new GoalTransitionCommand(GoalTestData.Profile, GoalTestData.Transition(created.Record, GoalStatus.Ready, "ready"), null, run.Authorization), TestContext.Current.CancellationToken);

        moved.ShouldBeOfType<GoalTransitioned>().Record.Goal.Status.ShouldBe(GoalStatus.Ready);
        sink.Seen.ShouldContain(static seen => seen.Kind == GoalEventKind.GoalTransitioned && seen.To == GoalStatus.Ready);
    }

    [Fact]
    public async Task TransitionAsync_WhenExpectedVersionIsStale_RejectsAsVersionConflict()
    {
        await using var harness = new DelegationHarness();
        var run = DelegationHarness.NewRun();
        var created = (GoalCreated) await harness.Goals.CreateAsync(Create(run), TestContext.Current.CancellationToken);
        _ = await harness.Goals.TransitionAsync(
            new GoalTransitionCommand(GoalTestData.Profile, GoalTestData.Transition(created.Record, GoalStatus.Ready, "ready"), null, run.Authorization), TestContext.Current.CancellationToken);
        var stale = GoalTestData.Transition(created.Record, GoalStatus.Cancelled, "stale", GoalTransitionReason.Cancelled);

        var result = await harness.Goals.TransitionAsync(new GoalTransitionCommand(GoalTestData.Profile, stale, null, run.Authorization), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<GoalTransitionRejected>().Failure.Kind.ShouldBe(GoalStoreFailureKind.VersionConflict);
    }

    [Fact]
    public async Task LoadAsync_WhenGoalDoesNotExist_RejectsAsNotFound()
    {
        await using var harness = new DelegationHarness();
        var run = DelegationHarness.NewRun();

        var result = await harness.Goals.LoadAsync(new GoalLoadCommand(GoalTestData.Profile, new GoalId(Guid.NewGuid()), run.Authorization), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<GoalLoadRejected>().Failure.Kind.ShouldBe(GoalStoreFailureKind.NotFound);
    }

    [Fact]
    public async Task ReadChildrenAsync_WhenAuthorityIsUnavailable_FailsClosed()
    {
        await using var harness = new DelegationHarness(configure: static services => services.AddSingleton<ISecurityAuthoritySelector, UnavailableSelector>());
        var run = DelegationHarness.NewRun();

        var result = await harness.Goals.ReadChildrenAsync(new GoalChildrenCommand(GoalTestData.Profile, new GoalId(Guid.NewGuid()), 0, 10, run.Authorization), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<GoalPageRejected>().Failure.Kind.ShouldBe(GoalStoreFailureKind.Unavailable);
    }

    [Fact]
    public async Task CreateAsync_WhenCancelled_EmitsCancelledActivityAndThrows()
    {
        await using var harness = new DelegationHarness();
        var run = DelegationHarness.NewRun();
        var command = Create(run);
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName == AgentKitActivityNames.GoalCreate && Equals(activity.GetTagItem(AgentKitTagNames.GoalId), command.Goal.Id.ToString()));
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await harness.Goals.CreateAsync(command, cancel.Token));

        activities.Snapshot().ShouldHaveSingleItem().Status.ShouldBe(ActivityStatusCode.Error);
    }

    private sealed class UnavailableSelector: ISecurityAuthoritySelector
    {
        public ValueTask<SecurityAuthoritySelectionResult> SelectAsync(SecurityAuthorizationContext authorization, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("selector down");
    }
}
