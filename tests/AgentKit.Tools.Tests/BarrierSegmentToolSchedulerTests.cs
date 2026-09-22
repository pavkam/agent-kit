// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
        await Task.Delay(50, TestContext.Current.CancellationToken);
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

    private static BarrierSegmentToolScheduler CreateScheduler(
        int maxParallel,
        UnknownSchedulingMode unknownSchedulingMode = UnknownSchedulingMode.Sequential) =>
        new(
            new ToolResultNormalizer(),
            Options.Create(new ToolRuntimeOptions
            {
                MaximumParallelInvocations = maxParallel,
                UnknownSchedulingMode = unknownSchedulingMode,
            }),
            TimeProvider.System,
            NullLogger<BarrierSegmentToolScheduler>.Instance);

    private static ToolBatch CreateBatch(
        ImmutableArray<ToolBatchEntry> entries,
        UnknownSchedulingMode unknownSchedulingMode = UnknownSchedulingMode.Sequential) =>
        new(
            TestAgentId,
            TestSessionId,
            TestRunId,
            entries,
            ToolBatchFailureMode.SettleIndependently,
            unknownSchedulingMode,
            DateTimeOffset.UnixEpoch.AddMinutes(5));

    private static ToolBatchEntry CreateEntry(
        int sourceOrdinal,
        SchedulingRecordingInvoker invoker,
        ToolExecutionHints hints,
        int callSuffix = 0)
    {
        var tool = Descriptor(hints);
        var callId = new ToolCallId(Guid.Parse($"aaaaaaaa-aaaa-aaaa-aaaa-{sourceOrdinal + callSuffix:D12}"));
        invoker.Bind(callId);
        var context = InvocationContext(tool, callId);
        var lease = new TestInvokerLease(tool, invoker);
        return new ToolBatchEntry(context, lease, hints, sourceOrdinal);
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

    private static ToolDescriptor Descriptor(ToolExecutionHints hints)
    {
        using var document = JsonDocument.Parse("{}");
        return new ToolDescriptor(
            new ToolId("tool"),
            new ToolVersion("1.0"),
            "tool",
            "Tool",
            new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), document.RootElement),
            outputSchema: null,
            new ToolEffects(ToolEffect.ReadOnly, null, null),
            hints,
            new ToolSourceId("agentkit.tools.tests"),
            ExtensionData.Empty);
    }

    private static ToolInvocationContext InvocationContext(ToolDescriptor tool, ToolCallId callId)
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
            new NoopProgressReporter());
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
