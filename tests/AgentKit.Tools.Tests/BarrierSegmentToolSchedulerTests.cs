// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies <see cref="BarrierSegmentToolScheduler"/> barrier segments, limits, and cancellation.</summary>
public sealed class BarrierSegmentToolSchedulerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenBatchIsEmpty_ReturnsEmptyResults()
    {
        var scheduler = CreateScheduler(maxParallel: 4);
        var batch = CreateBatch([]);

        var result = await scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken);

        result.Results.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenParallelCallsCompleteOutOfOrder_ReturnsResultsInSourceOrder()
    {
        var first = CreateRecordingInvoker(delayMs: 30, label: "first");
        var second = CreateRecordingInvoker(delayMs: 0, label: "second");
        var scheduler = CreateScheduler(maxParallel: 4);
        var batch = CreateBatch(
        [
            CreateEntry(0, first, ParallelHints()),
            CreateEntry(1, second, ParallelHints()),
        ]);

        var result = await scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken);

        result.Results.Length.ShouldBe(2);
        result.Results[0].CallId.ShouldBe(first.CallId);
        result.Results[1].CallId.ShouldBe(second.CallId);
        result.Results[0].Status.ShouldBe(ToolTerminalStatus.Succeeded);
        result.Results[1].Status.ShouldBe(ToolTerminalStatus.Succeeded);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSequentialCallSeparatesParallelSegments_RunsBarrierBetweenGroups()
    {
        var before = CreateRecordingInvoker(delayMs: 0, label: "before");
        var barrier = CreateRecordingInvoker(delayMs: 0, label: "barrier");
        var after = CreateRecordingInvoker(delayMs: 0, label: "after");
        var scheduler = CreateScheduler(maxParallel: 4);
        var batch = CreateBatch(
        [
            CreateEntry(0, before, ParallelHints()),
            CreateEntry(1, barrier, SequentialHints()),
            CreateEntry(2, after, ParallelHints()),
        ]);

        _ = await scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken);

        _ = barrier.StartedAt.ShouldNotBeNull();
        _ = before.CompletedAt.ShouldNotBeNull();
        _ = after.StartedAt.ShouldNotBeNull();
        before.CompletedAt!.Value.ShouldBeLessThan(barrier.StartedAt!.Value);
        barrier.CompletedAt!.Value.ShouldBeLessThan(after.StartedAt!.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallsShareConcurrencyKey_SerializesSameKeyOverlaps()
    {
        var first = CreateRecordingInvoker(delayMs: 40, label: "key-a-1");
        var second = CreateRecordingInvoker(delayMs: 0, label: "key-a-2");
        var third = CreateRecordingInvoker(delayMs: 0, label: "key-b");
        var scheduler = CreateScheduler(maxParallel: 4);
        var batch = CreateBatch(
        [
            CreateEntry(0, first, ConcurrencyHints("workspace")),
            CreateEntry(1, second, ConcurrencyHints("workspace")),
            CreateEntry(2, third, ConcurrencyHints("other")),
        ]);

        _ = await scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken);

        first.CompletedAt!.Value.ShouldBeLessThan(second.StartedAt!.Value);
        third.StartedAt!.Value.ShouldBeLessThan(second.StartedAt!.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMaximumParallelInvocationsIsOne_RunsParallelSegmentOneAtATime()
    {
        var first = CreateRecordingInvoker(delayMs: 30, label: "one");
        var second = CreateRecordingInvoker(delayMs: 0, label: "two");
        var scheduler = CreateScheduler(maxParallel: 1);
        var batch = CreateBatch(
        [
            CreateEntry(0, first, ParallelHints()),
            CreateEntry(1, second, ParallelHints()),
        ]);

        _ = await scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken);

        first.CompletedAt!.Value.ShouldBeLessThan(second.StartedAt!.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGlobalExclusiveCallPresent_RunsExclusiveCallAlone()
    {
        var parallelFirst = CreateRecordingInvoker(delayMs: 0, label: "parallel-first");
        var exclusive = CreateRecordingInvoker(delayMs: 0, label: "exclusive");
        var parallelSecond = CreateRecordingInvoker(delayMs: 0, label: "parallel-second");
        var scheduler = CreateScheduler(maxParallel: 4);
        var batch = CreateBatch(
        [
            CreateEntry(0, parallelFirst, ParallelHints()),
            CreateEntry(1, exclusive, GlobalExclusiveHints()),
            CreateEntry(2, parallelSecond, ParallelHints(), callSuffix: 2),
        ]);

        _ = await scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken);

        parallelFirst.CompletedAt!.Value.ShouldBeLessThan(exclusive.StartedAt!.Value);
        exclusive.CompletedAt!.Value.ShouldBeLessThan(parallelSecond.StartedAt!.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationRequestedMidSegment_MarksUnsettledCallsInterrupted()
    {
        var running = CreateRecordingInvoker(delayMs: Timeout.Infinite, label: "running");
        var waiting = CreateRecordingInvoker(delayMs: 0, label: "waiting");
        var scheduler = CreateScheduler(maxParallel: 1);
        var batch = CreateBatch(
        [
            CreateEntry(0, running, ParallelHints()),
            CreateEntry(1, waiting, ParallelHints(), callSuffix: 2),
        ]);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var executeTask = scheduler.ExecuteAsync(batch, cts.Token);
        var deadline = TimeProvider.System.GetUtcNow().AddSeconds(5);
        while (running.StartedAt is null && TimeProvider.System.GetUtcNow() < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        _ = running.StartedAt.ShouldNotBeNull();
        await cts.CancelAsync();

        var result = await executeTask;

        result.Results[0].Status.ShouldBe(ToolTerminalStatus.Interrupted);
        result.Results[1].Status.ShouldBe(ToolTerminalStatus.Interrupted);
        waiting.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnknownSchedulingModeIsReject_RejectsWithoutInvoking()
    {
        var invoker = CreateRecordingInvoker(delayMs: 0, label: "reject");
        var scheduler = CreateScheduler(maxParallel: 4, unknownSchedulingMode: UnknownSchedulingMode.Reject);
        var batch = CreateBatch([CreateEntry(0, invoker, UnspecifiedHints())], unknownSchedulingMode: UnknownSchedulingMode.Reject);

        var result = await scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Denied);
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReadOnlyCallFailsRetryably_RetriesWithPlannedBackoffUntilItSucceeds()
    {
        var fake = new FakeTimeProvider();
        var randomizers = new FixedRandomizerFactory();
        var scheduler = CreateScheduler(maxParallel: 4, timeProvider: fake, randomizers: randomizers);
        var invoker = new ScriptedAttemptInvoker(static context => context.Attempt < 3 ? Failure(SideEffectCertainty.DefinitelyNotPerformed, retryable: true) : Success());
        var retry = new ToolRetryPolicy(3, TimeSpan.FromMilliseconds(100), 2.0, TimeSpan.FromSeconds(1), 0.0);
        var batch = CreateBatch([CreateEntryFor(0, invoker, SequentialHints(), 0, effects: null, retry)], deadline: fake.GetUtcNow().AddMinutes(5));

        var result = await RunWithFakeTimeAsync(scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken), fake);

        var terminal = result.Results.ShouldHaveSingleItem();
        terminal.Status.ShouldBe(ToolTerminalStatus.Succeeded);
        invoker.Contexts.Select(static context => context.Attempt).ShouldBe([1, 2, 3]);
        (invoker.Contexts[1].InvocationStartedAt - invoker.Contexts[0].InvocationStartedAt).ShouldBeInRange(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150));
        (invoker.Contexts[2].InvocationStartedAt - invoker.Contexts[1].InvocationStartedAt).ShouldBeInRange(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(250));
        randomizers.Created.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptBudgetIsExhausted_ReturnsTheLastFailureWithAcceptanceEvidence()
    {
        var fake = new FakeTimeProvider();
        var scheduler = CreateScheduler(maxParallel: 4, timeProvider: fake);
        var invoker = new ScriptedAttemptInvoker(static _ => Failure(SideEffectCertainty.DefinitelyNotPerformed, retryable: true));
        var retry = new ToolRetryPolicy(2, TimeSpan.FromMilliseconds(10), 1.0, TimeSpan.FromMilliseconds(10), 0.0);
        var batch = CreateBatch([CreateEntryFor(0, invoker, SequentialHints(), 0, effects: null, retry)], deadline: fake.GetUtcNow().AddMinutes(5));

        var result = await RunWithFakeTimeAsync(scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken), fake);

        var terminal = result.Results.ShouldHaveSingleItem();
        terminal.Status.ShouldBe(ToolTerminalStatus.InvocationFailed);
        terminal.Retryable.ShouldBeTrue();
        _ = terminal.Acceptance.ShouldNotBeNull();
        _ = terminal.InvocationStartedAt.ShouldNotBeNull();
        invoker.Contexts.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFailureIsNotRetryable_DoesNotRetry()
    {
        var scheduler = CreateScheduler(maxParallel: 4);
        var invoker = new ScriptedAttemptInvoker(static _ => Failure(SideEffectCertainty.DefinitelyNotPerformed, retryable: false));
        var retry = new ToolRetryPolicy(5, TimeSpan.Zero, 1.0, TimeSpan.Zero, 0.0);
        var batch = CreateBatch([CreateEntryFor(0, invoker, SequentialHints(), 0, effects: null, retry)]);

        var result = await scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.InvocationFailed);
        invoker.Contexts.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMutatingCallMayHaveStartedAndInvokerDoesNotEnforceIdempotency_DoesNotRetry()
    {
        var scheduler = CreateScheduler(maxParallel: 4);
        var invoker = new ScriptedAttemptInvoker(static _ => Failure(SideEffectCertainty.Unknown, retryable: true));
        var retry = new ToolRetryPolicy(3, TimeSpan.Zero, 1.0, TimeSpan.Zero, 0.0);
        var effects = new ToolEffects(ToolEffect.Mutating, IdempotencyClassification.Idempotent, null);
        var batch = CreateBatch([CreateEntryFor(0, invoker, SequentialHints(), 0, effects, retry)]);

        var result = await scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.InvocationFailed);
        invoker.Contexts.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMutatingCallMayHaveStartedAndDeclaresNoIdempotency_DoesNotRetryEvenIfInvokerEnforces()
    {
        var scheduler = CreateScheduler(maxParallel: 4);
        var invoker = new EnforcingAttemptInvoker(enforces: true, static _ => Failure(SideEffectCertainty.Unknown, retryable: true));
        var retry = new ToolRetryPolicy(3, TimeSpan.Zero, 1.0, TimeSpan.Zero, 0.0);
        var effects = new ToolEffects(ToolEffect.Mutating, null, null);
        var batch = CreateBatch([CreateEntryFor(0, invoker, SequentialHints(), 0, effects, retry)]);

        var result = await scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken);

        var terminal = result.Results.ShouldHaveSingleItem();
        terminal.Retryable.ShouldBeFalse();
        invoker.Contexts.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyedIdempotentCallMayHaveStartedAndInvokerEnforcesIt_RetriesWithTheSameExternalKey()
    {
        var fake = new FakeTimeProvider();
        var scheduler = CreateScheduler(maxParallel: 4, timeProvider: fake);
        var invoker = new EnforcingAttemptInvoker(enforces: true, static context => context.Attempt == 1 ? Failure(SideEffectCertainty.Unknown, retryable: true) : Success());
        var retry = new ToolRetryPolicy(3, TimeSpan.FromMilliseconds(10), 1.0, TimeSpan.FromMilliseconds(10), 0.0);
        var effects = new ToolEffects(ToolEffect.Mutating, IdempotencyClassification.IdempotentWithKey, null);
        var batch = CreateBatch([CreateEntryFor(0, invoker, SequentialHints(), 0, effects, retry)], deadline: fake.GetUtcNow().AddMinutes(5));

        var result = await RunWithFakeTimeAsync(scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken), fake);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Succeeded);
        invoker.Contexts.Select(static context => context.ExternalIdempotencyKey).ShouldBe([new IdempotencyKey("key-1"), new IdempotencyKey("key-1")]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvokerDeclinesToEnforceIdempotency_DoesNotRetryAPossiblyStartedMutation()
    {
        var scheduler = CreateScheduler(maxParallel: 4);
        var invoker = new EnforcingAttemptInvoker(enforces: false, static _ => Failure(SideEffectCertainty.Unknown, retryable: true));
        var retry = new ToolRetryPolicy(3, TimeSpan.Zero, 1.0, TimeSpan.Zero, 0.0);
        var effects = new ToolEffects(ToolEffect.Mutating, IdempotencyClassification.Idempotent, null);
        var batch = CreateBatch([CreateEntryFor(0, invoker, SequentialHints(), 0, effects, retry)]);

        _ = await scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken);

        invoker.Contexts.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMutatingCallWasDefinitelyNotPerformed_RetriesUnderOrdinaryPolicy()
    {
        var fake = new FakeTimeProvider();
        var scheduler = CreateScheduler(maxParallel: 4, timeProvider: fake);
        var invoker = new ScriptedAttemptInvoker(static context => context.Attempt == 1 ? Failure(SideEffectCertainty.DefinitelyNotPerformed, retryable: true) : Success());
        var retry = new ToolRetryPolicy(2, TimeSpan.FromMilliseconds(10), 1.0, TimeSpan.FromMilliseconds(10), 0.0);
        var effects = new ToolEffects(ToolEffect.Mutating, null, null);
        var batch = CreateBatch([CreateEntryFor(0, invoker, SequentialHints(), 0, effects, retry)], deadline: fake.GetUtcNow().AddMinutes(5));

        var result = await RunWithFakeTimeAsync(scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken), fake);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Succeeded);
        invoker.Contexts.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBackoffWouldPassTheBatchDeadline_DoesNotRetry()
    {
        var fake = new FakeTimeProvider();
        var scheduler = CreateScheduler(maxParallel: 4, timeProvider: fake);
        var invoker = new ScriptedAttemptInvoker(static _ => Failure(SideEffectCertainty.DefinitelyNotPerformed, retryable: true));
        var retry = new ToolRetryPolicy(3, TimeSpan.FromMilliseconds(100), 1.0, TimeSpan.FromMilliseconds(100), 0.0);
        var batch = CreateBatch([CreateEntryFor(0, invoker, SequentialHints(), 0, effects: null, retry)], deadline: fake.GetUtcNow().AddMilliseconds(50));

        var result = await scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.InvocationFailed);
        invoker.Contexts.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJitterIsPlanned_AppliesTheInjectedRandomValueToTheBackoff()
    {
        var fake = new FakeTimeProvider();
        var randomizers = new FixedRandomizerFactory(unit: 1.0);
        var scheduler = CreateScheduler(maxParallel: 4, timeProvider: fake, randomizers: randomizers);
        var invoker = new ScriptedAttemptInvoker(static context => context.Attempt == 1 ? Failure(SideEffectCertainty.DefinitelyNotPerformed, retryable: true) : Success());
        var retry = new ToolRetryPolicy(2, TimeSpan.FromMilliseconds(1000), 1.0, TimeSpan.FromMilliseconds(1000), 0.5);
        var batch = CreateBatch([CreateEntryFor(0, invoker, SequentialHints(), 0, effects: null, retry)], deadline: fake.GetUtcNow().AddMinutes(5));

        _ = await RunWithFakeTimeAsync(scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken), fake);

        randomizers.Created.ShouldBe(1);
        var gap = invoker.Contexts[1].InvocationStartedAt - invoker.Contexts[0].InvocationStartedAt;
        gap.ShouldBeInRange(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(550));
    }

    [Fact]
    public async Task ExecuteAsync_WhenRetryIsScheduled_PublishesARetryEventAndSinkFailureDoesNotStopTheRetry()
    {
        var fake = new FakeTimeProvider();
        var collecting = new CollectingSink();
        var sinks = new[]
        {
            new ToolEventSinkBinding(new ToolEventSinkRegistration(new ComponentId("a-throwing"), 0), new ThrowingSink()),
            new ToolEventSinkBinding(new ToolEventSinkRegistration(new ComponentId("b-collecting"), 1), collecting),
        };
        var scheduler = CreateScheduler(maxParallel: 4, timeProvider: fake, sinks: sinks);
        var invoker = new ScriptedAttemptInvoker(static context => context.Attempt == 1 ? Failure(SideEffectCertainty.DefinitelyNotPerformed, retryable: true) : Success());
        var retry = new ToolRetryPolicy(2, TimeSpan.FromMilliseconds(10), 1.0, TimeSpan.FromMilliseconds(10), 0.0);
        var batch = CreateBatch([CreateEntryFor(0, invoker, SequentialHints(), 0, effects: null, retry)], deadline: fake.GetUtcNow().AddMinutes(5));

        var result = await RunWithFakeTimeAsync(scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken), fake);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Succeeded);
        var retryEvent = collecting.Events.ShouldHaveSingleItem().ShouldBeOfType<ToolRetryScheduledEvent>();
        retryEvent.FailedAttempt.ShouldBe(1);
        retryEvent.Delay.ShouldBe(TimeSpan.FromMilliseconds(10));
        retryEvent.CallId.ShouldBe(batch.Entries[0].Invocation.CallId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledDuringBackoff_ReturnsTheLastFailureWithoutAnotherAttempt()
    {
        var fake = new FakeTimeProvider();
        var scheduler = CreateScheduler(maxParallel: 4, timeProvider: fake);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var invoker = new ScriptedAttemptInvoker(_ =>
        {
            cancellation.Cancel();
            return Failure(SideEffectCertainty.DefinitelyNotPerformed, retryable: true);
        });
        var retry = new ToolRetryPolicy(3, TimeSpan.FromSeconds(10), 1.0, TimeSpan.FromSeconds(10), 0.0);
        var batch = CreateBatch([CreateEntryFor(0, invoker, SequentialHints(), 0, effects: null, retry)], deadline: fake.GetUtcNow().AddMinutes(5));

        var result = await scheduler.ExecuteAsync(batch, cancellation.Token);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.InvocationFailed);
        invoker.Contexts.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvokerThrows_ReportsUnknownCertaintyAndDoesNotRetry()
    {
        var scheduler = CreateScheduler(maxParallel: 4);
        var invoker = new ScriptedAttemptInvoker(static _ => throw new InvalidOperationException("secret detail"));
        var retry = new ToolRetryPolicy(3, TimeSpan.Zero, 1.0, TimeSpan.Zero, 0.0);
        var batch = CreateBatch([CreateEntryFor(0, invoker, SequentialHints(), 0, effects: null, retry)]);

        var result = await scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken);

        var terminal = result.Results.ShouldHaveSingleItem();
        terminal.Status.ShouldBe(ToolTerminalStatus.InvocationFailed);
        terminal.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
        terminal.Error!.SafeMessage.ShouldNotContain("secret detail");
        invoker.Contexts.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnAttemptIsInterrupted_ReturnsInterruptedWithUnknownCertaintyAndAcceptance()
    {
        var scheduler = CreateScheduler(maxParallel: 4);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var invoker = new CancellingInvoker(cancellation);
        var batch = CreateBatch([CreateEntryFor(0, invoker, SequentialHints(), 0, effects: null, retry: null)]);

        var result = await scheduler.ExecuteAsync(batch, cancellation.Token);

        var terminal = result.Results.ShouldHaveSingleItem();
        terminal.Status.ShouldBe(ToolTerminalStatus.Interrupted);
        terminal.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
        _ = terminal.Acceptance.ShouldNotBeNull();
        _ = terminal.InvocationStartedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAcceptedEntryNeverStarts_ReturnsDefinitelyNotPerformedWithAcceptanceAndNoStart()
    {
        var scheduler = CreateScheduler(maxParallel: 4);
        var invoker = CreateRecordingInvoker(delayMs: 0, label: "never");
        var batch = CreateBatch([CreateEntry(0, invoker, SequentialHints())]);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        var result = await scheduler.ExecuteAsync(batch, cancellation.Token);

        var terminal = result.Results.ShouldHaveSingleItem();
        terminal.Status.ShouldBe(ToolTerminalStatus.Interrupted);
        terminal.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        _ = terminal.Acceptance.ShouldNotBeNull();
        terminal.InvocationStartedAt.ShouldBeNull();
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRetryIsScheduled_RecordsBackoffActivityAndBoundedRetryMetric()
    {
        var fake = new FakeTimeProvider();
        var scheduler = CreateScheduler(maxParallel: 4, timeProvider: fake);
        var invoker = new ScriptedAttemptInvoker(static context => context.Attempt == 1 ? Failure(SideEffectCertainty.DefinitelyNotPerformed, retryable: true) : Success());
        var retry = new ToolRetryPolicy(2, TimeSpan.FromMilliseconds(10), 1.0, TimeSpan.FromMilliseconds(10), 0.0);
        var entry = CreateEntryFor(0, invoker, SequentialHints(), 0, effects: null, retry);
        var callId = entry.Invocation.CallId.ToString();
        var batch = CreateBatch([entry], deadline: fake.GetUtcNow().AddMinutes(5));
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.ToolRetryBackoff
                && Equals(observation.GetTagItem(AgentKitTagNames.ToolCallId), callId));
        using var metrics = new MetricCollector(AgentKitMetricNames.ToolRetryCount);

        _ = await RunWithFakeTimeAsync(scheduler.ExecuteAsync(batch, TestContext.Current.CancellationToken), fake);

        activities.Snapshot().ShouldHaveSingleItem().Status.ShouldBe(ActivityStatusCode.Ok);
        metrics.Snapshot().ShouldContain(static observation => Equals(observation.Tags[AgentKitTagNames.Outcome], "scheduled"));
    }

    private static async Task<ToolBatchResult> RunWithFakeTimeAsync(Task<ToolBatchResult> task, FakeTimeProvider fake)
    {
        while (!task.IsCompleted)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            fake.Advance(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
        }

        return await task;
    }

    private static ToolInvocationResult Failure(SideEffectCertainty certainty, bool retryable) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, certainty, retryable, "The attempt failed.", ExtensionData.Empty),
        []);

    private static ToolInvocationResult Success() => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, retryable: false, null, ExtensionData.Empty),
        [new TextPart("ok", TextSemantics.Plain, ExtensionData.Empty)]);

    private sealed class ScriptedAttemptInvoker(Func<ToolInvocationContext, ToolInvocationResult> script): IToolInvoker
    {
        public List<ToolInvocationContext> Contexts { get; } = [];

        public ValueTask<ToolInvocationResult> InvokeAsync(ToolInvocationContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            return ValueTask.FromResult(script(context));
        }
    }

    private sealed class EnforcingAttemptInvoker(bool enforces, Func<ToolInvocationContext, ToolInvocationResult> script): IIdempotencyEnforcingToolInvoker
    {
        public List<ToolInvocationContext> Contexts { get; } = [];

        public bool EnforcesIdempotency(ToolInvocationContext context) => enforces;

        public ValueTask<ToolInvocationResult> InvokeAsync(ToolInvocationContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            return ValueTask.FromResult(script(context));
        }
    }

    private sealed class CancellingInvoker(CancellationTokenSource source): IToolInvoker
    {
        public async ValueTask<ToolInvocationResult> InvokeAsync(ToolInvocationContext context, CancellationToken cancellationToken = default)
        {
            await source.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return Success();
        }
    }

    private sealed class CollectingSink: IToolEventSink
    {
        public List<ToolEvent> Events { get; } = [];

        public ValueTask PublishAsync(ToolEvent toolEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(toolEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingSink: IToolEventSink
    {
        public ValueTask PublishAsync(ToolEvent toolEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("sink failure");
    }

    private static BarrierSegmentToolScheduler CreateScheduler(
        int maxParallel,
        UnknownSchedulingMode unknownSchedulingMode = UnknownSchedulingMode.Sequential,
        TimeProvider? timeProvider = null,
        IEnumerable<ToolEventSinkBinding>? sinks = null,
        FixedRandomizerFactory? randomizers = null)
    {
        var options = Options.Create(new ToolRuntimeOptions
        {
            MaximumParallelInvocations = maxParallel,
            UnknownSchedulingMode = unknownSchedulingMode,
        });
        var clock = timeProvider ?? TimeProvider.System;
        return new BarrierSegmentToolScheduler(
            new ToolResultNormalizer(Options.Create(new ToolRuntimeOptions())),
            options,
            clock,
            new ToolEventDispatcher(sinks ?? [], options, clock, NullLogger<ToolEventDispatcher>.Instance),
            randomizers ?? new FixedRandomizerFactory(),
            NullLogger<BarrierSegmentToolScheduler>.Instance);
    }

    private static ToolBatch CreateBatch(
        ImmutableArray<ToolBatchEntry> entries,
        UnknownSchedulingMode unknownSchedulingMode = UnknownSchedulingMode.Sequential,
        DateTimeOffset? deadline = null) =>
        new(
            TestAgentId,
            TestSessionId,
            TestRunId,
            entries,
            ToolBatchFailureMode.SettleIndependently,
            unknownSchedulingMode,
            deadline ?? DateTimeOffset.UnixEpoch.AddMinutes(5));

    private static ToolBatchEntry CreateEntry(
        int sourceOrdinal,
        SchedulingRecordingInvoker invoker,
        ToolExecutionHints hints,
        int callSuffix = 0) =>
        CreateEntryFor(sourceOrdinal, invoker, hints, callSuffix, effects: null, retry: null);

    private static ToolBatchEntry CreateEntryFor(
        int sourceOrdinal,
        IToolInvoker invoker,
        ToolExecutionHints hints,
        int callSuffix,
        ToolEffects? effects,
        ToolRetryPolicy? retry)
    {
        var tool = Descriptor(hints, effects);
        var callId = new ToolCallId(Guid.Parse($"aaaaaaaa-aaaa-aaaa-aaaa-{sourceOrdinal + callSuffix:D12}"));
        if (invoker is SchedulingRecordingInvoker recording)
        {
            recording.Bind(callId);
        }

        var key = tool.Effects.Idempotency is IdempotencyClassification.IdempotentWithKey ? new IdempotencyKey("key-1") : (IdempotencyKey?) null;
        var context = InvocationContext(tool, callId, key);
        var authorization = context.InvocationGrant.Authorization;
        var policy = new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(1));
        var normalization = ToolRuntimeNormalizationDefaults.ForResolvedTool(policy);
        var fingerprint = new InputFingerprint("sha256:validated");
        var validated = new ValidatedToolCall(
            context.AgentId, context.SessionId, context.RunId, context.TurnId, context.OperationId, callId, authorization,
            new ToolCatalogVersion("catalog-1"), new ToolAlias("tool"), tool, tool.Version, policy, sourceOrdinal, context.Arguments,
            fingerprint, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var plan = new ToolExecutionPlan(hints, retry ?? ToolRetryPolicy.NoRetry, TimeSpan.FromMinutes(1), normalization);
        var accepted = new AcceptedToolCall(
            context.AgentId, context.SessionId, context.RunId, context.TurnId, context.OperationId, callId, authorization,
            new ToolCallAcceptanceEvidence(context.InvocationGrant.Id, fingerprint, DateTimeOffset.UnixEpoch),
            new ToolAlias("tool"), tool.Id, tool.Version, tool.Effects, key,
            new ToolCallAdmissionEvidence(new ToolCatalogVersion("catalog-1"), sourceOrdinal, new InputFingerprint("sha256:raw")),
            normalization, normalization.ProjectionPolicy, DateTimeOffset.UnixEpoch);
        return new ToolBatchEntry(context, new TestInvokerLease(tool, invoker), new PreparedToolCall(validated, plan), accepted);
    }

    private static ToolExecutionHints ParallelHints() =>
        new(ToolSchedulingMode.ParallelSafe, null, null, null);

    private static ToolExecutionHints SequentialHints() =>
        new(ToolSchedulingMode.Sequential, null, null, null);

    private static ToolExecutionHints GlobalExclusiveHints() =>
        new(ToolSchedulingMode.GlobalExclusive, null, null, null);

    private static ToolExecutionHints ConcurrencyHints(string key) =>
        new(ToolSchedulingMode.ConcurrencyKey, key, null, null);

    private static ToolExecutionHints UnspecifiedHints() =>
        new(ToolSchedulingMode.Unspecified, null, null, null);

    private static ToolDescriptor Descriptor(ToolExecutionHints hints, ToolEffects? effects = null)
    {
        using var document = JsonDocument.Parse("{}");
        return new ToolDescriptor(
            new ToolId("tool"),
            new ToolVersion("1.0"),
            "tool",
            "Tool",
            new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), document.RootElement),
            outputSchema: null,
            effects ?? new ToolEffects(ToolEffect.ReadOnly, null, null),
            hints,
            new ToolSourceId("agentkit.tools.tests"),
            ExtensionData.Empty);
    }

    private static ToolInvocationContext InvocationContext(ToolDescriptor tool, ToolCallId callId, IdempotencyKey? key = null)
    {
        var agentId = TestAgentId;
        var sessionId = TestSessionId;
        var runId = TestRunId;
        var turnId = TestTurnId;
        var operationId = TestOperationId;
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var correlation = new InRunOperationCorrelation(operationId, runId, turnId);
        var authorization = TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity);
        var grant = new SecurityGrant(
            new GrantId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            new SecurityRequestId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")),
            authorization.Scope,
            identity,
            authorization,
            ToolInvocationSecurityBinding.SecurityAudience,
            SecurityOperationKind.StateRead,
            SecurityEffect.Observe,
            [ToolInvocationSecurityBinding.Resource(tool.Id, tool.Version)],
            ToolInvocationSecurityBinding.InvocationFingerprint(callId, new InputFingerprint("sha256:test")),
            authorization.PolicySnapshot.Version,
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            1);
        return new ToolInvocationContext(
            agentId,
            sessionId,
            runId,
            turnId,
            operationId,
            callId,
            tool,
            tool.Version,
            JsonDocument.Parse("{}").RootElement,
            grant,
            attempt: 1,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            new NoopProgressReporter(),
            sessionProfile: null,
            key);
    }

    private static SchedulingRecordingInvoker CreateRecordingInvoker(int delayMs, string label) =>
        new(delayMs, label);

    private static readonly AgentId TestAgentId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly SessionId TestSessionId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly RunId TestRunId = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private static readonly TurnId TestTurnId = new(Guid.Parse("55555555-5555-5555-5555-555555555555"));
    private static readonly OperationId TestOperationId = new(Guid.Parse("44444444-4444-4444-4444-444444444444"));

    private sealed class NoopProgressReporter: IToolProgressReporter
    {
        public ValueTask ReportAsync(string message, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class TestInvokerLease(ToolDescriptor tool, IToolInvoker invoker): IToolInvokerLease
    {
        public ToolDescriptor Tool { get; } = tool;
        public ToolSourceVersion SourceVersion { get; } = new("1");
        public IToolInvoker Invoker { get; } = invoker;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SchedulingRecordingInvoker(int delayMs, string label): IToolInvoker
    {
        public ToolCallId CallId { get; private set; }
        public int Invocations { get; private set; }
        public DateTimeOffset? StartedAt { get; private set; }
        public DateTimeOffset? CompletedAt { get; private set; }

        public void Bind(ToolCallId callId) => CallId = callId;

        public async ValueTask<ToolInvocationResult> InvokeAsync(
            ToolInvocationContext context,
            CancellationToken cancellationToken = default)
        {
            Invocations++;
            StartedAt ??= TimeProvider.System.GetUtcNow();
            if (delayMs == Timeout.Infinite)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            else if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }

            CompletedAt = TimeProvider.System.GetUtcNow();
            return new ToolInvocationResult(
                new ToolCallOutcome(
                    ToolCallOutcomeKind.Success,
                    ToolTerminalStatus.Succeeded,
                    SideEffectCertainty.DefinitelyPerformed,
                    retryable: false,
                    null,
                    ExtensionData.Empty),
                [new TextPart(label, TextSemantics.Plain, ExtensionData.Empty)]);
        }
    }
}
