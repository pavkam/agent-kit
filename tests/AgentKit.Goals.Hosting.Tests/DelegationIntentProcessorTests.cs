// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting.Tests;

using AgentKit.Goals.Tests;

public sealed class DelegationIntentProcessorTests
{
    private static readonly ComponentId _scanner = new("tests.scanner");

    /// <summary>Discards signals so these tests, which drive the processor by hand, never race a self-starting worker.</summary>
    private sealed class InertSignal: IDelegationIntentSignal
    {
        public ValueTask SignalAsync(DelegationIntent intent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private static DelegationHarness Compose(ScriptedChildRunner runner, AgentId target, int slots = 4) => new(
        configure: services =>
        {
            _ = services.AddGoalDelegationWorker(options =>
            {
                options.MaximumConcurrentChildren = slots;
                options.ScannerId = _scanner;
            });
            _ = services.AddSingleton<IDelegationChildRunner>(runner);
            _ = services.ReplaceDelegationIntentSignal<InertSignal>();
        },
        targetAgents: [target],
        store: options => options.AuthorizedIntentScanners.Add(_scanner));

    /// <summary>Starts a delegation with no worker running and returns the ready child it committed.</summary>
    private static async Task<(DelegationHarness.RunContext Run, GoalRecord Child, Task<DelegationResult> Pending, CancellationTokenSource Cancel)> DelegateAsync(DelegationHarness harness, AgentId target, string key = "k", TimeSpan? deadline = null)
    {
        var run = DelegationHarness.NewRun();
        var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, key, deadline: deadline), hooks: null, cancel.Token);
        var child = await harness.WaitForChildAsync(run);
        return (run, child, pending, cancel);
    }

    private static Task<string> Drain(DelegationHarness harness, GoalRecord child, CancellationToken token) =>
        harness.Get<DelegationIntentProcessor>().ProcessAsync(child.Delegation!, child.Goal.Id, token);

    [Fact]
    public async Task ProcessAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var runner = new ScriptedChildRunner();
        await using var harness = Compose(runner, GoalTestData.NewAgent());

        (await Should.ThrowAsync<ArgumentNullException>(() => harness.Get<DelegationIntentProcessor>().ProcessAsync(null!, new GoalId(Guid.NewGuid()), TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task ProcessAsync_WhenDispatcherCommittedTheIntent_TheChildHasNotRunUntilTheWorkerDrainsIt()
    {
        var target = GoalTestData.NewAgent();
        var runner = new ScriptedChildRunner();
        await using var harness = Compose(runner, target);

        var (_, child, pending, cancel) = await DelegateAsync(harness, target);

        child.Goal.Status.ShouldBe(GoalStatus.Ready);
        runner.Runs.ShouldBe(0);
        pending.IsCompleted.ShouldBeFalse();
        await cancel.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => pending);
        cancel.Dispose();
    }

    [Fact]
    public async Task ProcessAsync_WhenChildIsReady_ClaimsRunsAndSettlesItAsCompleted()
    {
        var target = GoalTestData.NewAgent();
        var runner = new ScriptedChildRunner();
        await using var harness = Compose(runner, target);
        var (run, child, pending, cancel) = await DelegateAsync(harness, target);

        var outcome = await Drain(harness, child, TestContext.Current.CancellationToken);
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        var result = await pending;

        outcome.ShouldBe("completed");
        var succeeded = result.ShouldBeOfType<DelegationChildResult>();
        succeeded.Status.ShouldBe(DelegationStatus.Succeeded);
        succeeded.Result!.Summary.ShouldBe("Child answer.");
        _ = succeeded.ChildRunId.ShouldNotBeNull();
        var settled = (await harness.ChildrenAsync(run)).Single();
        settled.Goal.Status.ShouldBe(GoalStatus.Completed);
        settled.Attempts.ShouldHaveSingleItem().Status.ShouldBe(GoalAttemptStatus.Succeeded);
        settled.Attempts[0].AgentId.ShouldBe(target);
        runner.Requests.ShouldHaveSingleItem().AttemptId.ShouldBe(settled.Attempts[0].Id);
        cancel.Dispose();
    }

    [Fact]
    public async Task ProcessAsync_WhenTheSameIntentIsDrainedConcurrentlyAndAgain_RunsTheChildExactlyOnce()
    {
        var target = GoalTestData.NewAgent();
        var release = new TaskCompletionSource();
        var runner = new ScriptedChildRunner
        {
            Behavior = async (_, _) =>
            {
                await release.Task;
                return new DelegationChildRunResult(null, DelegationStatus.Succeeded, "x", GoalBudgetUsage.None, SideEffectCertainty.DefinitelyNotPerformed);
            },
        };
        await using var harness = Compose(runner, target);
        var (_, child, pending, cancel) = await DelegateAsync(harness, target);

        var first = Drain(harness, child, TestContext.Current.CancellationToken);
        var concurrent = await Drain(harness, child, TestContext.Current.CancellationToken);
        release.SetResult();
        var firstOutcome = await first;
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        _ = await pending;
        var replay = await Drain(harness, child, TestContext.Current.CancellationToken);

        concurrent.ShouldBe("duplicate");
        firstOutcome.ShouldBe("completed");
        replay.ShouldBe("skipped");
        runner.Runs.ShouldBe(1);
        cancel.Dispose();
    }

    [Fact]
    public async Task ProcessAsync_WhenDeadlinePassedBeforeClaim_FailsTheChildWithoutProvisioningOrRunning()
    {
        var target = GoalTestData.NewAgent();
        var runner = new ScriptedChildRunner();
        await using var harness = Compose(runner, target);
        var (run, child, pending, cancel) = await DelegateAsync(harness, target, deadline: TimeSpan.FromMinutes(1));
        harness.Time.Advance(TimeSpan.FromMinutes(2));

        var outcome = await Drain(harness, child, TestContext.Current.CancellationToken);
        _ = await pending;

        outcome.ShouldBe("expired");
        runner.Runs.ShouldBe(0);
        runner.Provisions.ShouldBeEmpty();
        (await harness.ChildrenAsync(run)).Single().Goal.Status.ShouldBe(GoalStatus.Failed);
        cancel.Dispose();
    }

    [Fact]
    public async Task ProcessAsync_WhenTargetCannotProvisionASession_FailsTheChildWithoutAnAttempt()
    {
        var target = GoalTestData.NewAgent();
        var runner = new ScriptedChildRunner { Provisionable = false };
        await using var harness = Compose(runner, target);
        var (run, child, pending, cancel) = await DelegateAsync(harness, target);

        var outcome = await Drain(harness, child, TestContext.Current.CancellationToken);
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        var result = await pending;

        outcome.ShouldBe("unprovisioned");
        result.ShouldBeOfType<DelegationChildResult>().Status.ShouldBe(DelegationStatus.Failed);
        var settled = (await harness.ChildrenAsync(run)).Single();
        settled.Goal.Status.ShouldBe(GoalStatus.Failed);
        settled.Attempts.ShouldBeEmpty();
        cancel.Dispose();
    }

    [Fact]
    public async Task ProcessAsync_WhenRunnerFaults_SettlesTheAttemptFailedWithUnknownEffects()
    {
        var target = GoalTestData.NewAgent();
        var runner = new ScriptedChildRunner { Behavior = static (_, _) => throw new InvalidOperationException("boom") };
        await using var harness = Compose(runner, target);
        var (run, child, pending, cancel) = await DelegateAsync(harness, target);

        var outcome = await Drain(harness, child, TestContext.Current.CancellationToken);
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        _ = await pending;

        outcome.ShouldBe("failed");
        var settled = (await harness.ChildrenAsync(run)).Single();
        settled.Goal.Status.ShouldBe(GoalStatus.Failed);
        settled.Attempts.Single().Outcome!.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
        cancel.Dispose();
    }

    [Fact]
    public async Task ProcessAsync_WhenDeadlineElapsesWhileRunning_CancelsTheRunAndSettlesFailed()
    {
        var target = GoalTestData.NewAgent();
        var started = new TaskCompletionSource();
        var runner = new ScriptedChildRunner
        {
            Behavior = async (_, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                throw new InvalidOperationException("unreachable");
            },
        };
        await using var harness = Compose(runner, target);
        var (run, child, pending, cancel) = await DelegateAsync(harness, target, deadline: TimeSpan.FromMinutes(5));

        var draining = Drain(harness, child, TestContext.Current.CancellationToken);
        await started.Task;
        harness.Time.Advance(TimeSpan.FromMinutes(6));
        var outcome = await draining;
        _ = await pending;

        outcome.ShouldBe("failed");
        var settled = (await harness.ChildrenAsync(run)).Single();
        settled.Goal.Status.ShouldBe(GoalStatus.Failed);
        settled.Transitions[^1].Reason.ShouldBe(GoalTransitionReason.DeadlineExceeded);
        cancel.Dispose();
    }

    [Fact]
    public async Task ProcessAsync_WhenTheWorkerStopsMidRun_SettlesTheClaimedAttemptFailedAndPropagatesCancellation()
    {
        var target = GoalTestData.NewAgent();
        var started = new TaskCompletionSource();
        var runner = new ScriptedChildRunner
        {
            Behavior = async (_, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                throw new InvalidOperationException("unreachable");
            },
        };
        await using var harness = Compose(runner, target);
        var (run, child, pending, cancel) = await DelegateAsync(harness, target);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var draining = Drain(harness, child, stop.Token);
        await started.Task;
        await stop.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => draining);
        var settled = (await harness.ChildrenAsync(run)).Single();
        settled.Goal.Status.ShouldBe(GoalStatus.Failed);
        settled.Attempts.Single().Outcome!.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
        await cancel.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => pending);
        cancel.Dispose();
    }

    [Fact]
    public async Task ProcessAsync_WhenAttemptWasLeftRunningByAPreviousIncarnation_SettlesItFailedWithoutRerunning()
    {
        var target = GoalTestData.NewAgent();
        var runner = new ScriptedChildRunner();
        await using var harness = Compose(runner, target);
        var (run, child, pending, cancel) = await DelegateAsync(harness, target);
        var authorization = child.Delegation!.Authorization;
        var claimed = (GoalAttemptStarted) await harness.Goals.StartAttemptAsync(
            new GoalAttemptRequest(
                GoalTestData.Profile, child.Goal.Id, child.Goal.Version, 1, null, target, GoalTestData.NewSession(), GoalTestData.NewRun(), run.RunId,
                GoalTestData.NewOperation(), TransitionActor.Worker, child.Delegation.Budget, new IdempotencyKey("previous-incarnation"), authorization),
            TestContext.Current.CancellationToken);
        harness.Time.Advance(TimeSpan.FromMinutes(1));

        var outcome = await Drain(harness, claimed.Record, TestContext.Current.CancellationToken);
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        var result = await pending;

        outcome.ShouldBe("recovered");
        runner.Runs.ShouldBe(0);
        result.ShouldBeOfType<DelegationChildResult>().Status.ShouldBe(DelegationStatus.Failed);
        (await harness.ChildrenAsync(run)).Single().Attempts.Single().Outcome!.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
        cancel.Dispose();
    }

    [Fact]
    public async Task ProcessAsync_WhenAnotherLiveWorkerOwnsTheRunningAttempt_SkipsIt()
    {
        var target = GoalTestData.NewAgent();
        var runner = new ScriptedChildRunner();
        await using var harness = Compose(runner, target);
        var processor = harness.Get<DelegationIntentProcessor>();
        var (run, child, pending, cancel) = await DelegateAsync(harness, target);
        var authorization = child.Delegation!.Authorization;
        var claimed = (GoalAttemptStarted) await harness.Goals.StartAttemptAsync(
            new GoalAttemptRequest(
                GoalTestData.Profile, child.Goal.Id, child.Goal.Version, 1, null, target, GoalTestData.NewSession(), GoalTestData.NewRun(), run.RunId,
                GoalTestData.NewOperation(), TransitionActor.Worker, child.Delegation.Budget, new IdempotencyKey("live-other"), authorization),
            TestContext.Current.CancellationToken);

        var outcome = await processor.ProcessAsync(child.Delegation, child.Goal.Id, TestContext.Current.CancellationToken);

        outcome.ShouldBe("skipped");
        (await harness.ChildrenAsync(run)).Single().Goal.Status.ShouldBe(GoalStatus.Active);
        claimed.Record.Goal.Status.ShouldBe(GoalStatus.Active);
        await cancel.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => pending);
        cancel.Dispose();
    }

    [Fact]
    public async Task ProcessAsync_WhenDrained_EmitsDrainActivityAndMetricWithBoundedOutcome()
    {
        var target = GoalTestData.NewAgent();
        var runner = new ScriptedChildRunner();
        await using var harness = Compose(runner, target);
        var (_, child, pending, cancel) = await DelegateAsync(harness, target);
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName == AgentKitActivityNames.DelegationWorkerDrain && Equals(activity.GetTagItem(AgentKitTagNames.GoalId), child.Goal.Id.ToString()));
        using var metrics = new MetricCollector(AgentKitMetricNames.DelegationWorkerDrainCount);

        _ = await Drain(harness, child, TestContext.Current.CancellationToken);
        harness.Time.Advance(TimeSpan.FromSeconds(1));
        _ = await pending;

        activities.Snapshot().ShouldHaveSingleItem().Status.ShouldBe(ActivityStatusCode.Ok);
        metrics.Snapshot().ShouldContain(static observation => Equals(observation.Tags[AgentKitTagNames.Outcome], "completed"));
        cancel.Dispose();
    }
}
