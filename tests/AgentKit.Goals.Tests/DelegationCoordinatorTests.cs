// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

public sealed class DelegationCoordinatorTests
{
    [Fact]
    public async Task DelegateAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        await using var harness = new DelegationHarness();

        var exception = await Should.ThrowAsync<ArgumentNullException>(() => harness.Delegation.DelegateAsync(null!, hooks: null, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task DelegateAsync_WhenChildSettles_ReturnsSucceededResultFromDurableState()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(targetAgents: [target]);
        var run = DelegationHarness.NewRun();
        var request = DelegationHarness.RequestFor(run, target, "one");

        var pending = harness.Delegation.DelegateAsync(request, hooks: null, TestContext.Current.CancellationToken);
        var child = await harness.WaitForChildAsync(run);
        child.Goal.Status.ShouldBe(GoalStatus.Ready);
        child.Goal.ParentId.ShouldBe(RunRootGoal.GoalIdFor(run.RunId));
        _ = await harness.CompleteChildAsync(child, "Found it.");
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        var result = await pending;

        var succeeded = result.ShouldBeOfType<DelegationChildResult>();
        succeeded.Status.ShouldBe(DelegationStatus.Succeeded);
        succeeded.Result!.Summary.ShouldBe("Found it.");
        succeeded.ChildGoalId.ShouldBe(child.Goal.Id);
        _ = succeeded.ChildRunId.ShouldNotBeNull();
    }

    [Fact]
    public async Task DelegateAsync_WhenRetriedWithSameKey_ResolvesTheSingleChild()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(targetAgents: [target]);
        var run = DelegationHarness.NewRun();
        var request = DelegationHarness.RequestFor(run, target, "retry");
        var first = harness.Delegation.DelegateAsync(request, hooks: null, TestContext.Current.CancellationToken);
        var child = await harness.WaitForChildAsync(run);
        _ = await harness.CompleteChildAsync(child);
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        _ = await first;

        var retried = await harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "retry"), hooks: null, TestContext.Current.CancellationToken);

        retried.ShouldBeOfType<DelegationChildResult>().Status.ShouldBe(DelegationStatus.Succeeded);
        (await harness.ChildrenAsync(run)).Length.ShouldBe(1);
    }

    [Fact]
    public async Task DelegateAsync_WhenTargetIsNotPublished_RejectsAsUnknownTarget()
    {
        await using var harness = new DelegationHarness(targetAgents: []);
        var run = DelegationHarness.NewRun();

        var result = await harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, GoalTestData.NewAgent(), "x"), hooks: null, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DelegationRejected>().Rejection.Kind.ShouldBe(DelegationRejectionKind.UnknownTarget);
        (await harness.ChildrenAsync(run)).ShouldBeEmpty();
    }

    [Fact]
    public async Task DelegateAsync_WhenAuthorityDenies_RejectsUnauthorizedBeforeAnyChildExists()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(targetAgents: [target]);
        harness.Authority.Deny = true;
        var run = DelegationHarness.NewRun();

        var result = await harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "deny"), hooks: null, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<DelegationRejected>();
        rejected.Rejection.Kind.ShouldBeOneOf(DelegationRejectionKind.Unauthorized, DelegationRejectionKind.InvalidRequest);
        (await harness.ChildrenAsync(run)).ShouldBeEmpty();
    }

    [Fact]
    public async Task DelegateAsync_WhenDeadlineAlreadyPassed_RejectsDeadlineElapsed()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(targetAgents: [target]);
        var run = DelegationHarness.NewRun();

        var result = await harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "late", deadline: TimeSpan.Zero), hooks: null, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DelegationRejected>().Rejection.Kind.ShouldBe(DelegationRejectionKind.DeadlineElapsed);
    }

    [Fact]
    public async Task DelegateAsync_WhenChildBudgetIsNotNarrowerThanParent_RejectsUnauthorized()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(targetAgents: [target], profile: static options => options.RootBudget = new GoalBudget(5, 10, 2));
        var run = DelegationHarness.NewRun();

        var result = await harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "wide", new GoalBudget(50, 10, 0)), hooks: null, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DelegationRejected>().Rejection.Kind.ShouldBe(DelegationRejectionKind.Unauthorized);
    }

    [Fact]
    public async Task DelegateAsync_WhenJoinStrategyIsNotAllowedByProfile_RejectsInvalidRequest()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(
            targetAgents: [target],
            profile: static options =>
            {
                options.JoinStrategies.Clear();
                options.JoinStrategies.Add(GoalJoinStrategyKeys.Quorum);
            });
        var run = DelegationHarness.NewRun();

        var result = await harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "join"), hooks: null, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DelegationRejected>().Rejection.Kind.ShouldBe(DelegationRejectionKind.InvalidRequest);
    }

    [Fact]
    public async Task DelegateAsync_WhenChildCeilingIsReached_RejectsLimitExceeded()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(targetAgents: [target], profile: static options => options.MaximumChildrenPerGoal = 1);
        var run = DelegationHarness.NewRun();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var first = harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "a"), hooks: null, cancel.Token);
        _ = await harness.WaitForChildAsync(run);

        var second = await harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "b"), hooks: null, TestContext.Current.CancellationToken);

        second.ShouldBeOfType<DelegationRejected>().Rejection.Kind.ShouldBe(DelegationRejectionKind.LimitExceeded);
        await cancel.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => first);
    }

    [Fact]
    public async Task DelegateAsync_WhenWaitIsCancelled_CancelsTheChildAndPropagatesCancellation()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(targetAgents: [target]);
        var run = DelegationHarness.NewRun();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "cancel"), hooks: null, cancel.Token);
        var child = await harness.WaitForChildAsync(run);

        await cancel.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => pending);
        (await harness.ChildrenAsync(run)).Single(record => record.Goal.Id == child.Goal.Id).Goal.Status.ShouldBe(GoalStatus.Cancelled);
    }

    [Fact]
    public async Task DelegateAsync_WhenDeadlineElapsesWhileWaiting_ReturnsDispatchedWithoutResult()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(targetAgents: [target]);
        var run = DelegationHarness.NewRun();
        var pending = harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "slow", deadline: TimeSpan.FromSeconds(5)), hooks: null, TestContext.Current.CancellationToken);
        _ = await harness.WaitForChildAsync(run);

        harness.Time.Advance(TimeSpan.FromSeconds(30));
        var result = await pending;

        var child = result.ShouldBeOfType<DelegationChildResult>();
        child.Status.ShouldBe(DelegationStatus.Dispatched);
        child.Result.ShouldBeNull();
    }

    [Fact]
    public async Task DelegateAsync_WhenChildFails_ReturnsFailedResult()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(targetAgents: [target]);
        var run = DelegationHarness.NewRun();
        var pending = harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "fail"), hooks: null, TestContext.Current.CancellationToken);
        var child = await harness.WaitForChildAsync(run);

        _ = await harness.CompleteChildAsync(child, end: GoalStatus.Failed);
        harness.Time.Advance(TimeSpan.FromSeconds(1));

        (await pending).ShouldBeOfType<DelegationChildResult>().Status.ShouldBe(DelegationStatus.Failed);
    }

    [Fact]
    public async Task DelegateAsync_WhenRejected_EmitsErrorActivityLogAndMetricWithoutContent()
    {
        var logger = new RecordingLogger<DelegationCoordinator>();
        await using var harness = new DelegationHarness(configure: services => services.AddSingleton<ILogger<DelegationCoordinator>>(logger), targetAgents: []);
        var run = DelegationHarness.NewRun();
        var request = DelegationHarness.RequestFor(run, GoalTestData.NewAgent(), "obs");
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName == AgentKitActivityNames.DelegationDelegate && Equals(activity.GetTagItem(AgentKitTagNames.DelegationId), request.Id.ToString()));
        using var metrics = new MetricCollector(AgentKitMetricNames.DelegationCount);

        _ = await harness.Delegation.DelegateAsync(request, hooks: null, TestContext.Current.CancellationToken);

        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.GoalId).ShouldBe(request.ParentGoalId.ToString());
        logger.Snapshot().ShouldContain(static entry => entry.EventId.Id == 31003 && entry.Level == LogLevel.Warning);
        logger.Snapshot().ShouldAllBe(static entry => !entry.Message.Contains("Research the subtopic.", StringComparison.Ordinal));
        metrics.Snapshot().ShouldContain(static observation => Equals(observation.Tags[AgentKitTagNames.Outcome], "rejected_unknown_target"));
    }

    [Fact]
    public async Task DelegateAsync_WhenObservationFails_DoesNotChangeTheOutcome()
    {
        var logger = new RecordingLogger<DelegationCoordinator> { ThrowOnWrite = true };
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(configure: services => services.AddSingleton<ILogger<DelegationCoordinator>>(logger), targetAgents: []);
        var run = DelegationHarness.NewRun();

        var result = await harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "obs"), hooks: null, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DelegationRejected>().Rejection.Kind.ShouldBe(DelegationRejectionKind.UnknownTarget);
    }

    [Fact]
    public async Task DelegateAsync_WhenChildIsDispatched_EmitsDispatchActivityParentedUnderDelegate()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(targetAgents: [target]);
        var run = DelegationHarness.NewRun();
        var request = DelegationHarness.RequestFor(run, target, "span");
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName is AgentKitActivityNames.DelegationDispatch or AgentKitActivityNames.DelegationDelegate
                && (Equals(activity.GetTagItem(AgentKitTagNames.DelegationId), request.Id.ToString())
                    || Equals(activity.GetTagItem(AgentKitTagNames.DelegationId), DelegationIdentity.DelegationId(request.ParentGoalId, request.IdempotencyKey).ToString())));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = harness.Delegation.DelegateAsync(request, hooks: null, cancel.Token);
        _ = await harness.WaitForChildAsync(run);

        await cancel.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => pending);

        var seen = activities.Snapshot();
        seen.ShouldContain(static activity => activity.OperationName == AgentKitActivityNames.DelegationDispatch && activity.Status == ActivityStatusCode.Ok);
        seen.Single(static activity => activity.OperationName == AgentKitActivityNames.DelegationDelegate).Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task DelegateAsync_WhenWaiting_ParksWorkerOccupancyForTheWaitingSession()
    {
        var target = GoalTestData.NewAgent();
        var parking = new RecordingParking();
        await using var harness = new DelegationHarness(configure: services => services.AddSingleton<IDelegationWaitParking>(parking), targetAgents: [target]);
        var run = DelegationHarness.NewRun();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "park"), hooks: null, cancel.Token);
        _ = await harness.WaitForChildAsync(run);

        parking.Active.ShouldBe(1);
        await cancel.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => pending);

        parking.Active.ShouldBe(0);
        parking.Sessions.ShouldBe([run.SessionId]);
    }

    private sealed class RecordingParking: IDelegationWaitParking
    {
        private int _active;

        internal int Active => Volatile.Read(ref _active);

        internal ConcurrentQueue<SessionId> Sessions { get; } = [];

        public ValueTask<IAsyncDisposable> ParkAsync(SessionId sessionId, CancellationToken cancellationToken = default)
        {
            _ = Interlocked.Increment(ref _active);
            Sessions.Enqueue(sessionId);
            return ValueTask.FromResult<IAsyncDisposable>(new Release(this));
        }

        private sealed class Release(RecordingParking owner): IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                _ = Interlocked.Decrement(ref owner._active);
                return ValueTask.CompletedTask;
            }
        }
    }

    [Fact]
    public async Task DelegateAsync_WhenDispatcherGrantDoesNotMatchTheExactDelegation_RejectsUnauthorizedAndCancelsTheChild()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(targetAgents: [target]);
        harness.Authority.TamperDelegationGrants = true;
        var run = DelegationHarness.NewRun();

        var result = await harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "tamper"), hooks: null, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DelegationRejected>().Rejection.Kind.ShouldBe(DelegationRejectionKind.Unauthorized);
        (await harness.ChildrenAsync(run)).ShouldHaveSingleItem().Goal.Status.ShouldBe(GoalStatus.Cancelled);
    }

    [Fact]
    public async Task DelegateAsync_WhenDispatched_SignalsTheHostWorkerWithTheReadyChild()
    {
        var target = GoalTestData.NewAgent();
        var signal = new CollectingSignal();
        await using var harness = new DelegationHarness(configure: services => services.AddSingleton<IDelegationIntentSignal>(signal), targetAgents: [target]);
        var run = DelegationHarness.NewRun();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "sig"), hooks: null, cancel.Token);
        var child = await harness.WaitForChildAsync(run);

        await cancel.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => pending);

        var intent = signal.Intents.ShouldHaveSingleItem();
        intent.Child.Goal.Id.ShouldBe(child.Goal.Id);
        intent.Child.Goal.Status.ShouldBe(GoalStatus.Ready);
        intent.Request.TargetAgentId.ShouldBe(target);
    }

    [Fact]
    public async Task DelegateAsync_WhenSignalFails_StillDispatchesBecauseTheIntentIsDurable()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(configure: static services => services.AddSingleton<IDelegationIntentSignal, ThrowingSignal>(), targetAgents: [target]);
        var run = DelegationHarness.NewRun();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "sigfail"), hooks: null, cancel.Token);

        var child = await harness.WaitForChildAsync(run);

        child.Goal.Status.ShouldBe(GoalStatus.Ready);
        await cancel.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => pending);
    }

    private sealed class CollectingSignal: IDelegationIntentSignal
    {
        internal ConcurrentQueue<DelegationIntent> Intents { get; } = [];

        public ValueTask SignalAsync(DelegationIntent intent, CancellationToken cancellationToken = default)
        {
            Intents.Enqueue(intent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingSignal: IDelegationIntentSignal
    {
        public ValueTask SignalAsync(DelegationIntent intent, CancellationToken cancellationToken = default) => throw new InvalidOperationException("signal down");
    }
}
