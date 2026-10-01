// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting.Tests;

using AgentKit.Goals.Tests;

public sealed class GoalDelegationWorkerTests
{
    private static readonly ComponentId _scanner = new("tests.scanner");

    private sealed class DroppingSignal: IDelegationIntentSignal
    {
        public ValueTask SignalAsync(DelegationIntent intent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private static DelegationHarness Compose(ScriptedChildRunner runner, AgentId target, int slots = 4, bool dropSignals = false, bool scanProfile = false, TimeSpan? scanInterval = null) => new(
        configure: services =>
        {
            _ = services.AddGoalDelegationWorker(options =>
            {
                options.MaximumConcurrentChildren = slots;
                options.ScannerId = _scanner;
                options.ScanInterval = scanInterval ?? TimeSpan.FromSeconds(30);
                if (scanProfile)
                {
                    options.Profiles.Add(GoalTestData.Profile);
                }
            });
            _ = services.AddSingleton<IDelegationChildRunner>(runner);
            if (dropSignals)
            {
                _ = services.ReplaceDelegationIntentSignal<DroppingSignal>();
            }
        },
        targetAgents: [target],
        store: options => options.AuthorizedIntentScanners.Add(_scanner));

    private static async Task<T> Pump<T>(DelegationHarness harness, Task<T> task)
    {
        for (var i = 0; i < 2000 && !task.IsCompleted; i++)
        {
            harness.Time.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }

        return await task;
    }

    [Fact]
    public async Task StartAsync_WhenDelegationIsSignalled_RunsTheChildInTheWorkerAndTheParentReceivesItsResult()
    {
        var target = GoalTestData.NewAgent();
        var runner = new ScriptedChildRunner();
        await using var harness = Compose(runner, target);
        var worker = harness.Get<GoalDelegationWorker>();
        await worker.StartAsync(TestContext.Current.CancellationToken);
        var run = DelegationHarness.NewRun();

        var result = await Pump(harness, harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "e2e"), hooks: null, TestContext.Current.CancellationToken));

        result.ShouldBeOfType<DelegationChildResult>().Status.ShouldBe(DelegationStatus.Succeeded);
        runner.Runs.ShouldBe(1);
        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartAsync_WhenTheSignalWasLost_TheScanRecoversTheDurableIntent()
    {
        var target = GoalTestData.NewAgent();
        var runner = new ScriptedChildRunner();
        await using var harness = Compose(runner, target, dropSignals: true, scanProfile: true, scanInterval: TimeSpan.FromSeconds(1));
        var worker = harness.Get<GoalDelegationWorker>();
        var run = DelegationHarness.NewRun();
        var pending = harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "scan"), hooks: null, TestContext.Current.CancellationToken);
        _ = await harness.WaitForChildAsync(run);
        runner.Runs.ShouldBe(0);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        var result = await Pump(harness, pending);

        result.ShouldBeOfType<DelegationChildResult>().Status.ShouldBe(DelegationStatus.Succeeded);
        runner.Runs.ShouldBe(1);
        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ScanOnceAsync_WhenScannerIsNotAuthorizedByTheStore_DrainsNothingAndDoesNotThrow()
    {
        var target = GoalTestData.NewAgent();
        var runner = new ScriptedChildRunner();
        await using var harness = new DelegationHarness(
            configure: services =>
            {
                _ = services.AddGoalDelegationWorker(options =>
                {
                    options.ScannerId = new ComponentId("untrusted");
                    options.Profiles.Add(GoalTestData.Profile);
                });
                _ = services.AddSingleton<IDelegationChildRunner>(runner);
                _ = services.ReplaceDelegationIntentSignal<DroppingSignal>();
            },
            targetAgents: [target]);
        var worker = harness.Get<GoalDelegationWorker>();
        var run = DelegationHarness.NewRun();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "deny"), hooks: null, cancel.Token);
        _ = await harness.WaitForChildAsync(run);

        await worker.ScanOnceAsync(TestContext.Current.CancellationToken);

        runner.Runs.ShouldBe(0);
        await cancel.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => pending);
    }

    private static DelegationRequest Nested(AgentId executing, SessionId session, RunId engineRun, AgentId target)
    {
        var run = new DelegationHarness.RunContext(executing, session, engineRun, GoalTestData.Authorization(executing, session, engineRun));
        return DelegationHarness.RequestFor(run, target, "nested");
    }

    [Fact]
    public async Task StartAsync_WhenOneSlotHostsANestedDelegation_TheWaitingParentReleasesItsSlotSoTheChildCanRun()
    {
        var first = GoalTestData.NewAgent();
        var second = GoalTestData.NewAgent();
        var runner = new ScriptedChildRunner();
        DelegationResult? innerResult = null;
        await using var harness = new DelegationHarness(
            configure: services =>
            {
                _ = services.AddGoalDelegationWorker(options => options.MaximumConcurrentChildren = 1);
                _ = services.AddSingleton<IDelegationChildRunner>(runner);
            },
            targetAgents: [first, second]);
        runner.Behavior = async (request, token) =>
        {
            var engineRun = new RunId(Guid.NewGuid());
            if (request.Delegation.TargetAgentId != first)
            {
                return new DelegationChildRunResult(engineRun, DelegationStatus.Succeeded, "grandchild", GoalBudgetUsage.None, SideEffectCertainty.DefinitelyNotPerformed);
            }

            var inner = await harness.Delegation.DelegateAsync(Nested(first, request.SessionId, engineRun, second), hooks: null, token);
            innerResult = inner;
            return new DelegationChildRunResult(engineRun, inner is DelegationChildResult { Status: DelegationStatus.Succeeded } ? DelegationStatus.Succeeded : DelegationStatus.Failed, "outer", GoalBudgetUsage.None, SideEffectCertainty.DefinitelyNotPerformed);
        };
        var worker = harness.Get<GoalDelegationWorker>();
        await worker.StartAsync(TestContext.Current.CancellationToken);
        var run = DelegationHarness.NewRun();

        var result = await Pump(harness, harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, first, "outer", new GoalBudget(5, 10, 2)), hooks: null, TestContext.Current.CancellationToken));

        innerResult.ShouldBeOfType<DelegationChildResult>().Status.ShouldBe(DelegationStatus.Succeeded);
        result.ShouldBeOfType<DelegationChildResult>().Status.ShouldBe(DelegationStatus.Succeeded);
        runner.Runs.ShouldBe(2);
        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StopAsync_WhenAChildIsRunning_SettlesItBeforeTheWorkerStops()
    {
        var target = GoalTestData.NewAgent();
        var started = new TaskCompletionSource();
        var runner = new ScriptedChildRunner
        {
            Behavior = async (request, token) =>
            {
                _ = started.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
                throw new InvalidOperationException("unreachable");
            },
        };
        await using var harness = Compose(runner, target);
        var worker = harness.Get<GoalDelegationWorker>();
        await worker.StartAsync(TestContext.Current.CancellationToken);
        var run = DelegationHarness.NewRun();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "stop"), hooks: null, cancel.Token);
        await started.Task;

        await worker.StopAsync(TestContext.Current.CancellationToken);

        worker.InFlight.ShouldBe(0);
        (await harness.ChildrenAsync(run)).Single().Goal.Status.ShouldBe(GoalStatus.Failed);
        await cancel.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task SignalAsync_WhenNobodyStartedTheWorker_TheFirstCommittedIntentStartsItOnce()
    {
        var target = GoalTestData.NewAgent();
        var runner = new ScriptedChildRunner();
        await using var harness = Compose(runner, target);
        var run = DelegationHarness.NewRun();

        var result = await Pump(harness, harness.Delegation.DelegateAsync(DelegationHarness.RequestFor(run, target, "lazy"), hooks: null, TestContext.Current.CancellationToken));

        result.ShouldBeOfType<DelegationChildResult>().Status.ShouldBe(DelegationStatus.Succeeded);
        runner.Runs.ShouldBe(1);
    }

    [Fact]
    public async Task StartAsync_WhenCalledTwice_StartsTheLoopsOnce()
    {
        var target = GoalTestData.NewAgent();
        var runner = new ScriptedChildRunner();
        await using var harness = Compose(runner, target);
        var worker = harness.Get<GoalDelegationWorker>();

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await worker.StartAsync(TestContext.Current.CancellationToken);

        await worker.DisposeAsync();
        worker.InFlight.ShouldBe(0);
    }

    [Fact]
    public async Task DisposeAsync_WhenNeverStarted_CompletesWithoutError()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = Compose(new ScriptedChildRunner(), target);

        await harness.Get<GoalDelegationWorker>().DisposeAsync();
    }
}
