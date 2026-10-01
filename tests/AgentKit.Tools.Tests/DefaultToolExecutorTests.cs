// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Collections.Immutable;
using System.Text.Json;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies <see cref="DefaultToolExecutor"/> sequential pipeline behavior.</summary>
public sealed class DefaultToolExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenAliasUnknown_ReturnsUnknownToolTerminalResult()
    {
        var executor = CreateExecutor(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult())));

        var request = CallRequest(new ToolAlias("missing-alias"));
        var result = await executor.ExecuteAsync(capture, [request], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results.Length.ShouldBe(1);
        result.Results[0].Status.ShouldBe(ToolTerminalStatus.UnknownTool);
        result.Results[0].ToolId.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenArgumentsInvalid_ReturnsInvalidArgumentsWithoutInvoking()
    {
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult()));
        var executor = CreateExecutor(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(invoker, RequiredPathSchema());

        var request = CallRequest(new ToolAlias("read"), "{}u8"u8.ToArray());
        var result = await executor.ExecuteAsync(capture, [request], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results[0].Status.ShouldBe(ToolTerminalStatus.InvalidArguments);
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthorizationDenied_ReturnsDeniedWithoutInvoking()
    {
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult()));
        var executor = CreateExecutor(new DenyInvocationAuthority());
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results[0].Status.ShouldBe(ToolTerminalStatus.Denied);
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthorizationDefersToApproval_RecordsTheWaitAndDeniesWithoutInvoking()
    {
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult()));
        var recorder = new RecordingApprovalWaitRecorder();
        var executor = CreateExecutor(new ApprovalRequiredInvocationAuthority(), recorder);
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results[0].Status.ShouldBe(ToolTerminalStatus.Denied);
        invoker.Invocations.ShouldBe(0);
        var (request, approval) = recorder.Waits.ShouldHaveSingleItem();
        request.Id.ShouldBe(new SecurityRequestId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
        approval.Binding.Request.ShouldBeSameAs(request);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthorizationDefersAndNoRecorderIsComposed_DeniesWithoutInvoking()
    {
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult()));
        var executor = CreateExecutor(new ApprovalRequiredInvocationAuthority());
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results[0].Status.ShouldBe(ToolTerminalStatus.Denied);
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthorizationIsDeniedOutright_RecordsNoApprovalWait()
    {
        var recorder = new RecordingApprovalWaitRecorder();
        var executor = CreateExecutor(new DenyInvocationAuthority(), recorder);
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult())));

        _ = await executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        recorder.Waits.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthorizationIsAllowed_RecordsNoApprovalWait()
    {
        var recorder = new RecordingApprovalWaitRecorder();
        var executor = CreateExecutor(new AllowInvocationAuthority(), recorder);
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult())));

        _ = await executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        recorder.Waits.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvokerThrows_ReturnsInvocationFailedTerminalResult()
    {
        var executor = CreateExecutor(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(new RecordingInvoker(static async (_, _) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("boom");
        }));

        var result = await executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results[0].Status.ShouldBe(ToolTerminalStatus.InvocationFailed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelled_PropagatesCancellation()
    {
        var executor = CreateExecutor(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(new RecordingInvoker(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
            return SuccessResult();
        }));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthorizedAndInvokerSucceeds_ReturnsSucceededTerminalResult()
    {
        var executor = CreateExecutor(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult("ok"))));

        var result = await executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results[0].Status.ShouldBe(ToolTerminalStatus.Succeeded);
        _ = result.Results[0].Acceptance.ShouldNotBeNull();
        var text = result.Results[0].Content.ShouldHaveSingleItem().ShouldBeOfType<ToolResultTextContent>();
        text.Text.ShouldBe("ok");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallIsAuthorized_RecordsAcceptedBeforeInvocationAndTerminalAfterIt()
    {
        var order = new List<string>();
        var invoker = new RecordingInvoker((_, _) =>
        {
            order.Add("invoke");
            return ValueTask.FromResult(SuccessResult("ok"));
        });
        var harness = CreateHarness(new AllowInvocationAuthority(), order: order);
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        order.ShouldBe(["accepted", "invoke", "terminal"]);
        var accepted = harness.Recorder.Accepted.ShouldHaveSingleItem();
        accepted.ToolId.ShouldBe(new ToolId("tool.read"));
        accepted.Normalization.ExecutionPolicy.ShouldBe(Standard);
        accepted.Acceptance.InvocationGrantId.ShouldBe(new GrantId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")));
        accepted.ExternalIdempotencyKey.ShouldBeNull();
        var terminal = harness.Recorder.Terminals.ShouldHaveSingleItem();
        terminal.ShouldBe(result.Results[0]);
        terminal.Acceptance.ShouldBe(accepted.Acceptance);
        harness.Recorder.Targets.ShouldAllBe(static target => target == Target);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAcceptedRecordIsRejected_FailsClosedWithoutInvoking()
    {
        var recorder = new ScriptedRecorder
        {
            AcceptedOutcome = static () => new ToolCallRecordRejected(ToolCallRecordRejectionKind.Unavailable, "store down"),
        };
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult()));
        var harness = CreateHarness(new AllowInvocationAuthority(), recorder: recorder);
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        var terminal = result.Results.ShouldHaveSingleItem();
        terminal.Status.ShouldBe(ToolTerminalStatus.Unsupported);
        terminal.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        terminal.Acceptance.ShouldBeNull();
        terminal.GrantId.ShouldBe(new GrantId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")));
        terminal.Error!.SafeMessage.ShouldNotContain("store down");
        invoker.Invocations.ShouldBe(0);
        recorder.Terminals.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Unsupported);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAcceptedRecorderThrows_FailsClosedWithoutInvoking()
    {
        var recorder = new ScriptedRecorder { AcceptedOutcome = static () => throw new InvalidOperationException("secret store detail") };
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult()));
        var harness = CreateHarness(new AllowInvocationAuthority(), recorder: recorder);
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        var terminal = result.Results.ShouldHaveSingleItem();
        terminal.Status.ShouldBe(ToolTerminalStatus.Unsupported);
        terminal.Error!.SafeMessage.ShouldNotContain("secret");
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAcceptedRecordingIsCancelled_PropagatesCancellationAndNeverInvokes()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var recorder = new ScriptedRecorder
        {
            AcceptedOutcome = () =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            },
        };
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult()));
        var harness = CreateHarness(new AllowInvocationAuthority(), recorder: recorder);
        await using var capture = await CreateCaptureAsync(invoker);

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), cancellation.Token));

        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallIsRejectedBeforeAcceptance_RecordsOnlyATerminalResult()
    {
        var harness = CreateHarness(new DenyInvocationAuthority());
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult())));

        var result = await harness.Executor.ExecuteAsync(
            capture,
            [CallRequest(new ToolAlias("missing"), callSuffix: 1), CallRequest(callSuffix: 2)],
            ExecutionCapability(),
            TestContext.Current.CancellationToken);

        result.Results.Select(static item => item.Status).ShouldBe([ToolTerminalStatus.UnknownTool, ToolTerminalStatus.Denied]);
        harness.Recorder.Accepted.ShouldBeEmpty();
        harness.Recorder.Terminals.Select(static item => item.Status).ShouldBe([ToolTerminalStatus.UnknownTool, ToolTerminalStatus.Denied]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTerminalRecordIsRejected_ReturnsTheUnchangedResultAndReportsItNotRecorded()
    {
        var sink = new CollectingSink();
        var recorder = new ScriptedRecorder
        {
            TerminalOutcome = static () => new ToolCallRecordRejected(ToolCallRecordRejectionKind.Conflict, "advanced"),
        };
        var harness = CreateHarness(
            new AllowInvocationAuthority(),
            recorder: recorder,
            sinks: [new ToolEventSinkBinding(new ToolEventSinkRegistration(new ComponentId("collect"), 0), sink)]);
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult("ok"))));

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Succeeded);
        var terminalEvent = sink.Events.OfType<ToolCallTerminalEvent>().ShouldHaveSingleItem();
        terminalEvent.Recorded.ShouldBeFalse();
        terminalEvent.Accepted.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTerminalRecorderThrows_ReturnsTheUnchangedResult()
    {
        var recorder = new ScriptedRecorder { TerminalOutcome = static () => throw new InvalidOperationException("store failure") };
        var harness = CreateHarness(new AllowInvocationAuthority(), recorder: recorder);
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult("ok"))));

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Succeeded);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelsDuringInvocation_StillRecordsTheTerminalResultWithoutTheCallerToken()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var recorder = new ScriptedRecorder();
        var invoker = new RecordingInvoker((_, _) =>
        {
            cancellation.Cancel();
            return ValueTask.FromResult(SuccessResult("ok"));
        });
        var harness = CreateHarness(new AllowInvocationAuthority(), recorder: recorder);
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), cancellation.Token);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Succeeded);
        _ = recorder.Terminals.ShouldHaveSingleItem();
        recorder.TerminalTokensCanBeCancelled.ShouldBe([false]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledAfterAnEarlierCallWasAccepted_SettlesThatCallWithAnInterruptedTerminalRecord()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var recorder = new ScriptedRecorder();
        recorder.AcceptedOutcomeFor = accepted =>
        {
            if (recorder.Accepted.Count < 2)
            {
                return null;
            }

            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        };
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult("ok")));
        var harness = CreateHarness(new AllowInvocationAuthority(), recorder: recorder);
        await using var capture = await CreateCaptureAsync(invoker);

        _ = await Should.ThrowAsync<OperationCanceledException>(() => harness.Executor.ExecuteAsync(
            capture,
            [CallRequest(callSuffix: 1), CallRequest(callSuffix: 2)],
            ExecutionCapability(),
            cancellation.Token));

        invoker.Invocations.ShouldBe(0);
        var settled = recorder.Terminals.ShouldHaveSingleItem();
        settled.CallId.ShouldBe(recorder.Accepted[0].CallId);
        settled.Status.ShouldBe(ToolTerminalStatus.Interrupted);
        settled.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        _ = settled.Acceptance.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledWhilePublishingTheAcceptedEvent_SettlesTheAcceptedCallWithoutInvoking()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var recorder = new ScriptedRecorder();
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult("ok")));
        var harness = CreateHarness(
            new AllowInvocationAuthority(),
            recorder: recorder,
            sinks: [new ToolEventSinkBinding(new ToolEventSinkRegistration(new ComponentId("cancelling"), 0), new CancellingSink(cancellation))]);
        await using var capture = await CreateCaptureAsync(invoker);

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), cancellation.Token));

        invoker.Invocations.ShouldBe(0);
        _ = recorder.Accepted.ShouldHaveSingleItem();
        var settled = recorder.Terminals.ShouldHaveSingleItem();
        settled.Status.ShouldBe(ToolTerminalStatus.Interrupted);
        settled.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthorizationFaults_RejectsBeforeAcceptanceWithoutLeakingDetail()
    {
        var harness = CreateHarness(new ThrowingAuthority());
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult()));
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        var terminal = result.Results.ShouldHaveSingleItem();
        terminal.Status.ShouldBe(ToolTerminalStatus.InvocationFailed);
        terminal.Error!.SafeMessage.ShouldNotContain("authority detail");
        harness.Recorder.Accepted.ShouldBeEmpty();
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPolicyReferenceIsNotBoundToTheRun_RejectsBeforeAcceptance()
    {
        var other = new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("other"), new ToolExecutionPolicyVersion(1));
        var harness = CreateHarness(new AllowInvocationAuthority());
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult()));
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(other), TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Unsupported);
        harness.Recorder.Accepted.ShouldBeEmpty();
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnlyAnotherRevisionOfThePolicyIsRegistered_RejectsWithoutFallingBack()
    {
        var newer = new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(2));
        var options = Options.Create(new ToolRuntimeOptions());
        var selector = new ToolExecutionPolicySelector(
            new Dictionary<ToolExecutionPolicyReference, IToolExecutionPolicy> { [newer] = new DefaultToolExecutionPolicy(newer, options) },
            NullLogger<ToolExecutionPolicySelector>.Instance);
        var harness = CreateHarness(new AllowInvocationAuthority(), selector: selector);
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult()));
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(Standard, newer), TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Unsupported);
        invoker.Invocations.ShouldBe(0);
        harness.Recorder.Accepted.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPolicyRefusesToPlan_RejectsTheCallWithoutLeakingTheReason()
    {
        var selector = new FixedSelector(new ScriptedPolicy(Standard, static (_, _) => new ToolExecutionPlanRejected("internal reason")));
        var harness = CreateHarness(new AllowInvocationAuthority(), selector: selector);
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult()));
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        var terminal = result.Results.ShouldHaveSingleItem();
        terminal.Status.ShouldBe(ToolTerminalStatus.Unsupported);
        terminal.Error!.SafeMessage.ShouldNotContain("internal reason");
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPolicyThrows_RejectsTheCall()
    {
        var selector = new FixedSelector(new ScriptedPolicy(Standard, static (_, _) => throw new InvalidOperationException("policy bug")));
        var harness = CreateHarness(new AllowInvocationAuthority(), selector: selector);
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult())));

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Unsupported);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPolicyCancels_PropagatesCancellation()
    {
        var selector = new FixedSelector(new ScriptedPolicy(Standard, static (_, token) => throw new OperationCanceledException(token)));
        var harness = CreateHarness(new AllowInvocationAuthority(), selector: selector);
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult())));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), cancellation.Token));
    }

    [Fact]
    public async Task ExecuteAsync_WhenPolicyReturnsAPlanForDifferentCalls_RejectsTheCall()
    {
        var selector = new FixedSelector(new ScriptedPolicy(Standard, static (_, _) => new ToolExecutionPlanned([])));
        var harness = CreateHarness(new AllowInvocationAuthority(), selector: selector);
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult())));

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Unsupported);
        harness.Recorder.Accepted.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPolicyPlansRetries_RetriesAReadOnlyCallThatFailsRetryably()
    {
        var invoker = new RecordingInvoker((context, _) => ValueTask.FromResult(
            context.Attempt < 2
                ? new ToolInvocationResult(
                    new ToolCallOutcome(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed, retryable: true, "transient", ExtensionData.Empty),
                    [])
                : SuccessResult("ok")));
        var harness = CreateHarness(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Succeeded);
        invoker.Invocations.ShouldBe(2);
        _ = harness.Recorder.Accepted.ShouldHaveSingleItem();
        _ = harness.Recorder.Terminals.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnknownSchedulingModeIsRejected_RejectsAnUnspecifiedCallBeforeAcceptance()
    {
        var options = new ToolRuntimeOptions { UnknownSchedulingMode = UnknownSchedulingMode.Reject };
        var harness = CreateHarness(new AllowInvocationAuthority(), options: options);
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult()));
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Denied);
        harness.Recorder.Accepted.ShouldBeEmpty();
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenToolDeclaresKeyedIdempotency_RecordsAndPassesAStableExternalKey()
    {
        var descriptor = KeyedDescriptor();
        ToolInvocationContext? seen = null;
        var invoker = new RecordingInvoker((context, _) =>
        {
            seen = context;
            return ValueTask.FromResult(SuccessResult("ok"));
        });
        var harness = CreateHarness(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(invoker, descriptor);

        _ = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        var accepted = harness.Recorder.Accepted.ShouldHaveSingleItem();
        accepted.ExternalIdempotencyKey.ShouldBe(new IdempotencyKey("agentkit.tool-call:33333333-3333-3333-3333-333333333333:66666666-6666-6666-6666-666666666666"));
        seen.ShouldNotBeNull().ExternalIdempotencyKey.ShouldBe(accepted.ExternalIdempotencyKey);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallSucceeds_PublishesAcceptedThenTerminalEventsAndSinkFailureChangesNothing()
    {
        var collecting = new CollectingSink();
        var harness = CreateHarness(
            new AllowInvocationAuthority(),
            sinks:
            [
                new ToolEventSinkBinding(new ToolEventSinkRegistration(new ComponentId("a-throwing"), 0), new ThrowingSink()),
                new ToolEventSinkBinding(new ToolEventSinkRegistration(new ComponentId("b-collecting"), 1), collecting),
            ]);
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult("ok"))));

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Succeeded);
        collecting.Events.Select(static item => item.GetType()).ShouldBe([typeof(ToolCallAcceptedEvent), typeof(ToolCallTerminalEvent)]);
        var accepted = collecting.Events[0].ShouldBeOfType<ToolCallAcceptedEvent>();
        accepted.ExecutionPolicy.ShouldBe(Standard);
        var terminal = collecting.Events[1].ShouldBeOfType<ToolCallTerminalEvent>();
        terminal.Status.ShouldBe(ToolTerminalStatus.Succeeded);
        terminal.Recorded.ShouldBeTrue();
        terminal.Accepted.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAcceptedRecordFails_PublishesNoAcceptedEvent()
    {
        var collecting = new CollectingSink();
        var recorder = new ScriptedRecorder { AcceptedOutcome = static () => new ToolCallRecordRejected(ToolCallRecordRejectionKind.Unavailable, "down") };
        var harness = CreateHarness(
            new AllowInvocationAuthority(),
            recorder: recorder,
            sinks: [new ToolEventSinkBinding(new ToolEventSinkRegistration(new ComponentId("collect"), 0), collecting)]);
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult())));

        _ = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        collecting.Events.ShouldHaveSingleItem().ShouldBeOfType<ToolCallTerminalEvent>().Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenBatchMixesOutcomes_ReturnsOneTerminalResultPerCallInSourceOrder()
    {
        var harness = CreateHarness(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult("ok"))));

        var result = await harness.Executor.ExecuteAsync(
            capture,
            [CallRequest(new ToolAlias("missing"), callSuffix: 1), CallRequest(callSuffix: 2), CallRequest(callSuffix: 3)],
            ExecutionCapability(),
            TestContext.Current.CancellationToken);

        result.Results.Select(static item => item.Status).ShouldBe([ToolTerminalStatus.UnknownTool, ToolTerminalStatus.Succeeded, ToolTerminalStatus.Succeeded]);
        harness.Recorder.Accepted.Count.ShouldBe(2);
        harness.Recorder.Terminals.Count.ShouldBe(3);
        harness.Recorder.Terminals.Select(static item => item.CallId).Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallRunsWithSecretArguments_NoSignalContainsTheArguments()
    {
        const string secret = "sensitive-token-value";
        var logger = new RecordingLogger<DefaultToolExecutor>();
        var harness = CreateHarness(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult("ok"))));
        using var metrics = new MetricCollector(AgentKitMetricNames.ToolEventPublishCount);
        var arguments = System.Text.Encoding.UTF8.GetBytes($"{{\"token\":\"{secret}\"}}");

        _ = await harness.Executor.ExecuteAsync(capture, [CallRequest(rawArguments: arguments)], ExecutionCapability(), TestContext.Current.CancellationToken);

        SignalAssertions.ShouldNotContainContent([], logger.Snapshot(), metrics.Snapshot(), secret);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRunIsBudgeted_HoldsAStartedConcurrentSlotAroundTheInvocationAndAccountsTheSuccess()
    {
        var scope = new RecordingBudgetScope();
        bool? heldDuringInvocation = null;
        var invoker = new RecordingInvoker((_, _) =>
        {
            var slot = scope.Reservations.Single(static item => item.Dimension == BudgetDimensions.ConcurrentToolCalls);
            heldDuringInvocation = slot.Started && slot.Committed is null;
            return ValueTask.FromResult(SuccessResult("ok"));
        });
        var harness = CreateHarness(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], BudgetedCapability(scope), TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Succeeded);
        heldDuringInvocation.ShouldBe(true);
        var slot = scope.Reservations.Single(static item => item.Dimension == BudgetDimensions.ConcurrentToolCalls);
        slot.Committed.ShouldBe(1m);
        scope.Reservations.Single(static item => item.Dimension == BudgetDimensions.SuccessfulToolCalls).Committed.ShouldBe(1m);
        scope.Reservations.Single(static item => item.Dimension == BudgetDimensions.ToolResultBytes).Committed.ShouldBe(2m);
        scope.Reservations.Select(static item => item.Request.IdempotencyKey.Value).Distinct().Count().ShouldBe(scope.Reservations.Count);
        scope.Reservations.ShouldAllBe(static item => item.Request.IdempotencyKey.Value.StartsWith("tool:33333333-3333-3333-3333-333333333333:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheConcurrentBudgetIsRefused_RejectsTheAcceptedCallWithoutInvokingIt()
    {
        var scope = new RecordingBudgetScope();
        _ = scope.Refused.Add(BudgetDimensions.ConcurrentToolCalls);
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult("ok")));
        var harness = CreateHarness(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], BudgetedCapability(scope), TestContext.Current.CancellationToken);

        var terminal = result.Results.ShouldHaveSingleItem();
        terminal.Status.ShouldBe(ToolTerminalStatus.ResourceLimitExceeded);
        terminal.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        invoker.Invocations.ShouldBe(0);
        _ = harness.Recorder.Accepted.ShouldHaveSingleItem();
        _ = harness.Recorder.Terminals.ShouldHaveSingleItem();
        scope.Reservations.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheBudgetLedgerCannotAnswer_FailsTheCallClosedWithoutInvokingIt()
    {
        var scope = new RecordingBudgetScope { Unavailable = true };
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult("ok")));
        var harness = CreateHarness(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], BudgetedCapability(scope), TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.ResourceLimitExceeded);
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAReadOnlyCallIsRetried_CountsEachRetryOnTheRetriesDimension()
    {
        var scope = new RecordingBudgetScope();
        var invoker = new RecordingInvoker((context, _) => ValueTask.FromResult(
            context.Attempt < 3 ? RetryableFailure() : SuccessResult("ok")));
        var harness = CreateHarness(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], BudgetedCapability(scope), TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Succeeded);
        invoker.Invocations.ShouldBe(3);
        scope.Reservations.Where(static item => item.Dimension == BudgetDimensions.ToolRetries).Select(static item => item.Committed)
            .ShouldBe([1m, 1m]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheRetryBudgetIsRefused_DeclinesTheRetryAndReturnsTheFailure()
    {
        var scope = new RecordingBudgetScope();
        _ = scope.Refused.Add(BudgetDimensions.ToolRetries);
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(RetryableFailure()));
        var harness = CreateHarness(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], BudgetedCapability(scope), TestContext.Current.CancellationToken);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.InvocationFailed);
        invoker.Invocations.ShouldBe(1);
        scope.Reservations.Single(static item => item.Dimension == BudgetDimensions.ConcurrentToolCalls).Committed.ShouldBe(1m);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnAttemptReachesItsDeadlineAndHonorsCancellation_ReportsTimedOutAndSignalsTheToken()
    {
        var time = new FakeTimeProvider();
        var options = new ToolRuntimeOptions { MaximumAttempts = 1, InvocationTimeout = TimeSpan.FromSeconds(30) };
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sawCancellation = false;
        var invoker = new RecordingInvoker(async (_, token) =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException)
            {
                sawCancellation = true;
                throw;
            }

            return SuccessResult();
        });
        var harness = CreateHarness(new AllowInvocationAuthority(), options: options, time: time);
        await using var capture = await CreateCaptureAsync(invoker);

        var pending = harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);
        await started.Task;
        time.Advance(TimeSpan.FromSeconds(30));
        var result = await pending;

        var terminal = result.Results.ShouldHaveSingleItem();
        terminal.Status.ShouldBe(ToolTerminalStatus.TimedOut);
        terminal.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
        terminal.Retryable.ShouldBeTrue();
        sawCancellation.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnAttemptIgnoresCancellationThroughTheDrainPeriod_AbandonsItAndKeepsItsConcurrentSlot()
    {
        var time = new FakeTimeProvider();
        var options = new ToolRuntimeOptions
        {
            MaximumAttempts = 1,
            InvocationTimeout = TimeSpan.FromSeconds(30),
            InvocationDrainPeriod = TimeSpan.FromSeconds(5),
        };
        var scope = new RecordingBudgetScope();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource<ToolInvocationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var invoker = new RecordingInvoker((_, _) =>
        {
            started.SetResult();
            return new ValueTask<ToolInvocationResult>(never.Task);
        });
        var harness = CreateHarness(new AllowInvocationAuthority(), options: options, time: time);
        await using var capture = await CreateCaptureAsync(invoker);

        var pending = harness.Executor.ExecuteAsync(capture, [CallRequest()], BudgetedCapability(scope), TestContext.Current.CancellationToken);
        await started.Task;
        time.Advance(TimeSpan.FromSeconds(30));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        pending.IsCompleted.ShouldBeFalse();
        time.Advance(TimeSpan.FromSeconds(5));
        var result = await pending;
        never.SetException(new InvalidOperationException("late failure is observed and ignored"));

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.TimedOut);
        var slot = scope.Reservations.Single(static item => item.Dimension == BudgetDimensions.ConcurrentToolCalls);
        slot.Started.ShouldBeTrue();
        slot.Committed.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnAttemptFinishesWithinTheDrainAfterItsDeadline_KeepsItsOwnSuccessfulResult()
    {
        var time = new FakeTimeProvider();
        var options = new ToolRuntimeOptions { MaximumAttempts = 1, InvocationTimeout = TimeSpan.FromSeconds(30) };
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invoker = new RecordingInvoker(async (_, token) =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException)
            {
            }

            return SuccessResult("finished");
        });
        var harness = CreateHarness(new AllowInvocationAuthority(), options: options, time: time);
        await using var capture = await CreateCaptureAsync(invoker);

        var pending = harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);
        await started.Task;
        time.Advance(TimeSpan.FromSeconds(30));
        var result = await pending;

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Succeeded);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAReadOnlyAttemptTimesOutAndAttemptsRemain_RetriesIt()
    {
        var time = new FakeTimeProvider();
        var options = new ToolRuntimeOptions
        {
            MaximumAttempts = 2,
            InvocationTimeout = TimeSpan.FromSeconds(30),
            RetryInitialDelay = TimeSpan.Zero,
            RetryMaximumDelay = TimeSpan.Zero,
            RetryJitterFraction = 0.0,
        };
        var invoker = new RecordingInvoker(async (context, token) =>
        {
            if (context.Attempt == 1)
            {
                await Task.Delay(Timeout.Infinite, token);
            }

            return SuccessResult("second");
        });
        var harness = CreateHarness(new AllowInvocationAuthority(), options: options, time: time);
        await using var capture = await CreateCaptureAsync(invoker);

        var pending = harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);
        while (!pending.IsCompleted)
        {
            time.Advance(TimeSpan.FromSeconds(30));
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        (await pending).Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.Succeeded);
        invoker.Invocations.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAMutatingAttemptTimesOutWithUnknownCertainty_IsNotRetried()
    {
        var time = new FakeTimeProvider();
        var options = new ToolRuntimeOptions { MaximumAttempts = 3, InvocationTimeout = TimeSpan.FromSeconds(30) };
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invoker = new RecordingInvoker(async (context, token) =>
        {
            _ = started.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return SuccessResult();
        });
        var harness = CreateHarness(new AllowInvocationAuthority(), options: options, time: time);
        await using var capture = await CreateCaptureAsync(invoker, MutatingDescriptor());

        var pending = harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);
        await started.Task;
        time.Advance(TimeSpan.FromSeconds(30));
        var result = await pending;

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(ToolTerminalStatus.TimedOut);
        invoker.Invocations.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAComposedSpillStoresAnOversizedResult_ReturnsABoundedPreviewAndTheArtifactReference()
    {
        var reference = TestArtifactReference();
        var spill = new RecordingSpill(new ToolResultSpilled(reference));
        var options = new ToolRuntimeOptions { MaximumResultBytes = 16, ResultSpillPreviewBytes = 8 };
        var harness = CreateHarness(new AllowInvocationAuthority(), options: options, spill: spill);
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult(new string('x', 100)))));

        var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        var terminal = result.Results.ShouldHaveSingleItem();
        terminal.Status.ShouldBe(ToolTerminalStatus.Succeeded);
        terminal.Content.Length.ShouldBe(2);
        terminal.Content[0].ShouldBeOfType<ToolResultTextContent>().Text.ShouldBe("xxxxxxxx");
        terminal.Content[1].ShouldBeOfType<ToolResultArtifactContent>().Reference.ShouldBe(reference);
        spill.Requests.ShouldHaveSingleItem().Content.Length.ShouldBe(100);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoSpillIsComposedOrItIsRefused_TruncatesTheOversizedResult()
    {
        var options = new ToolRuntimeOptions { MaximumResultBytes = 16 };
        var refused = new RecordingSpill(new ToolResultNotSpilled("refused"));
        foreach (var spill in new IToolResultSpill?[] { null, refused })
        {
            var harness = CreateHarness(new AllowInvocationAuthority(), options: options, spill: spill);
            await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult(new string('x', 100)))));

            var result = await harness.Executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

            var content = result.Results.ShouldHaveSingleItem().Content.ShouldHaveSingleItem().ShouldBeOfType<ToolResultTextContent>();
            content.Text.Length.ShouldBe(16);
        }

        _ = refused.Requests.ShouldHaveSingleItem();
    }

    private static ToolInvocationResult RetryableFailure() => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed, retryable: true, "transient", ExtensionData.Empty),
        []);

    private static ToolDescriptor MutatingDescriptor()
    {
        var baseline = ToolCaptureTestData.Descriptor();
        return new ToolDescriptor(
            baseline.Id, baseline.Version, baseline.Name, baseline.Description, baseline.InputSchema, baseline.OutputSchema,
            new ToolEffects(ToolEffect.Mutating, null, null),
            baseline.ExecutionHints, baseline.SourceId, baseline.Extensions);
    }

    private static ArtifactReference TestArtifactReference() => new(
        new ArtifactId(Guid.Parse("a0000000-0000-0000-0000-0000000000a1")),
        new ArtifactVersion("1"),
        new ArtifactDirectoryId("tool-results"),
        new ArtifactProfileKey("artifacts"),
        new ArtifactProfileVersion(1),
        new TenantId("tenant"),
        new ArtifactOwnerId("session:test"),
        new PrincipalId("principal"),
        "text/plain",
        100,
        new ArtifactIntegrity(FileSecurityBinding.ContentFingerprint("x"u8), DateTimeOffset.UnixEpoch),
        DataClassification.Internal,
        ArtifactOwnershipKind.Session,
        ArtifactMutability.Immutable,
        new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false),
        null,
        DateTimeOffset.UnixEpoch);

    private sealed class RecordingSpill(ToolResultSpillResult outcome): IToolResultSpill
    {
        public List<ToolResultSpillRequest> Requests { get; } = [];

        public ValueTask<ToolResultSpillResult> SpillAsync(ToolResultSpillRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.FromResult(outcome);
        }
    }

    private static ToolDescriptor KeyedDescriptor()
    {
        var baseline = ToolCaptureTestData.Descriptor();
        return new ToolDescriptor(
            baseline.Id, baseline.Version, baseline.Name, baseline.Description, baseline.InputSchema, baseline.OutputSchema,
            new ToolEffects(ToolEffect.Mutating, IdempotencyClassification.IdempotentWithKey, null),
            baseline.ExecutionHints, baseline.SourceId, baseline.Extensions);
    }

    private sealed record Harness(DefaultToolExecutor Executor, ScriptedRecorder Recorder);

    private sealed class ScriptedRecorder(List<string>? order = null): IToolCallRecorder
    {
        public List<AcceptedToolCall> Accepted { get; } = [];
        public List<ToolCallResult> Terminals { get; } = [];
        public List<ToolCallSessionTarget> Targets { get; } = [];
        public List<bool> TerminalTokensCanBeCancelled { get; } = [];
        public Func<ToolCallRecordResult>? AcceptedOutcome { get; set; }
        public Func<AcceptedToolCall, ToolCallRecordResult?>? AcceptedOutcomeFor { get; set; }
        public Func<ToolCallRecordResult>? TerminalOutcome { get; set; }

        public ValueTask<ToolCallRecordResult> RecordAcceptedAsync(
            AcceptedToolCall accepted, SessionExecutionCapability session, ToolCallSessionTarget target, CancellationToken cancellationToken = default)
        {
            order?.Add("accepted");
            Targets.Add(target);
            Accepted.Add(accepted);
            var scripted = AcceptedOutcomeFor?.Invoke(accepted);
            return ValueTask.FromResult(scripted ?? AcceptedOutcome?.Invoke() ?? new ToolCallRecorded());
        }

        public ValueTask<ToolCallRecordResult> RecordTerminalAsync(
            ToolCallResult result, SessionExecutionCapability session, ToolCallSessionTarget target, CancellationToken cancellationToken = default)
        {
            order?.Add("terminal");
            Targets.Add(target);
            Terminals.Add(result);
            TerminalTokensCanBeCancelled.Add(cancellationToken.CanBeCanceled);
            return ValueTask.FromResult(TerminalOutcome?.Invoke() ?? new ToolCallRecorded());
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

    private sealed class CancellingSink(CancellationTokenSource source): IToolEventSink
    {
        public async ValueTask PublishAsync(ToolEvent toolEvent, CancellationToken cancellationToken = default)
        {
            await source.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private sealed class ThrowingSink: IToolEventSink
    {
        public ValueTask PublishAsync(ToolEvent toolEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("sink failure");
    }

    private sealed class ScriptedPolicy(
        ToolExecutionPolicyReference reference,
        Func<ImmutableArray<ValidatedToolCall>, CancellationToken, ToolExecutionPlanResult> script): IToolExecutionPolicy
    {
        public ToolExecutionPolicyReference Reference { get; } = reference;

        public ValueTask<ToolExecutionPlanResult> PlanAsync(
            ImmutableArray<ValidatedToolCall> calls, ToolExecutionPolicyContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(script(calls, cancellationToken));
    }

    private sealed class FixedSelector(IToolExecutionPolicy policy): IToolExecutionPolicySelector
    {
        public ValueTask<ToolExecutionPolicySelectionResult> SelectAsync(
            ToolExecutionPolicyReference reference, ToolExecutionCapability capability, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ToolExecutionPolicySelectionResult>(new ToolExecutionPolicySelected(policy));
    }

    private sealed class ThrowingAuthority: ISecurityAuthority
    {
        public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("authority detail");
    }

    private static DefaultToolExecutor CreateExecutor(ISecurityAuthority authority, IApprovalWaitRecorder? approvalWaits = null) =>
        CreateHarness(authority, approvalWaits: approvalWaits).Executor;

    private static Harness CreateHarness(
        ISecurityAuthority authority,
        IApprovalWaitRecorder? approvalWaits = null,
        ScriptedRecorder? recorder = null,
        IToolExecutionPolicySelector? selector = null,
        ToolRuntimeOptions? options = null,
        IEnumerable<ToolEventSinkBinding>? sinks = null,
        List<string>? order = null,
        TimeProvider? time = null,
        IToolResultSpill? spill = null)
    {
        var clock = time ?? TimeProvider.System;
        var limits = new ToolSchemaLimits(262_144, 64, 10_000, 100_000);
        var schemaEngine = new BoundedToolSchemaEngine(
            TimeProvider.System,
            NullLogger<BoundedToolSchemaEngine>.Instance,
            NullLogger<CompiledToolSchema>.Instance);
        var runtimeOptions = Options.Create(options ?? new ToolRuntimeOptions { RetryInitialDelay = TimeSpan.Zero, RetryMaximumDelay = TimeSpan.Zero, RetryJitterFraction = 0.0 });
        var events = new ToolEventDispatcher(sinks ?? [], runtimeOptions, clock, NullLogger<ToolEventDispatcher>.Instance);
        var normalizer = new ToolResultNormalizer(runtimeOptions);
        var scheduler = new BarrierSegmentToolScheduler(
            normalizer,
            runtimeOptions,
            clock,
            events,
            new FixedRandomizerFactory(),
            NullLogger<BarrierSegmentToolScheduler>.Instance);
        var effectiveRecorder = recorder ?? new ScriptedRecorder(order);
        var effectiveSelector = selector ?? StandardSelector(runtimeOptions);
        var executor = new DefaultToolExecutor(
            new ToolCallResolver(TimeProvider.System, NullLogger<ToolCallResolver>.Instance),
            new ToolArgumentValidator(schemaEngine, TimeProvider.System),
            effectiveSelector,
            new FixedSecurityAuthoritySelector(authority),
            new FixedSecurityRequestIdGenerator(),
            effectiveRecorder,
            scheduler,
            events,
            limits,
            runtimeOptions,
            clock,
            NullLogger<DefaultToolExecutor>.Instance,
            approvalWaits: approvalWaits,
            resultSpill: spill);
        return new Harness(executor, effectiveRecorder);
    }

    private static ToolExecutionPolicySelector StandardSelector(IOptions<ToolRuntimeOptions> options) =>
        new(
            new Dictionary<ToolExecutionPolicyReference, IToolExecutionPolicy>
            {
                [Standard] = new DefaultToolExecutionPolicy(Standard, options),
            },
            NullLogger<ToolExecutionPolicySelector>.Instance);

    private static ToolExecutionPolicyReference Standard { get; } =
        new(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(1));

    private static async Task<ToolCatalogCapture> CreateCaptureAsync(
        RecordingInvoker invoker,
        ToolDescriptor? descriptor = null,
        ToolExecutionPolicyReference? policy = null)
    {
        var tool = descriptor ?? ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var bindings = ToolCaptureTestData.Bindings(tool, invoker);
        var provider = new StaticToolProvider(
            new ToolProviderBindings(publication, bindings),
            TimeProvider.System,
            NullLogger<StaticToolProvider>.Instance,
            NullLogger<ToolProviderCapture>.Instance);
        var providerCapture = await provider.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);
        var alias = new ToolAlias("read");
        var identity = new ToolIdentity(tool.Id, tool.Version);
        var discovery = ToolCaptureTestData.Discovery();
        var snapshot = new ToolCatalogSnapshot(
            discovery.AgentId,
            discovery.SessionId,
            discovery.RunId,
            discovery.Identity,
            discovery.Authorization.PolicySnapshot,
            discovery.Authorization.AgentDefinitionRevision,
            discovery.Authorization.ConfigurationVersion,
            new ToolCatalogVersion("catalog-1"),
            ImmutableDictionary.Create<ToolSourceId, ToolSourceVersion>().Add(publication.SourceId, publication.SourceVersion),
            [tool],
            ImmutableDictionary.Create<ToolIdentity, ToolExecutionPolicyReference>().Add(identity, policy ?? Standard),
            ImmutableDictionary.Create<ToolAlias, ToolIdentity>().Add(alias, identity));
        return new ToolCatalogCapture(
            snapshot,
            ImmutableDictionary.Create<ToolSourceId, IToolProviderCapture>().Add(publication.SourceId, providerCapture),
            TimeProvider.System,
            NullLogger<ToolCatalogCapture>.Instance);
    }

    private static ToolCallRequest CallRequest(ToolAlias? alias = null, byte[]? rawArguments = null, int callSuffix = 6)
    {
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var sessionId = new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var runId = new RunId(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var turnId = new TurnId(Guid.Parse("55555555-5555-5555-5555-555555555555"));
        var operationId = new OperationId(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var correlation = new InRunOperationCorrelation(operationId, runId, turnId);
        var authorization = TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity);
        return new ToolCallRequest(
            agentId,
            sessionId,
            runId,
            turnId,
            operationId,
            new ToolCallId(Guid.Parse($"66666666-6666-6666-6666-66666666666{callSuffix}")),
            authorization,
            new ToolCatalogVersion("catalog-1"),
            sourceOrdinal: 0,
            alias ?? new ToolAlias("read"),
            rawArguments is null ? [123, 125] : [.. rawArguments],
            DateTimeOffset.UnixEpoch);
    }

    private static ToolDescriptor RequiredPathSchema()
    {
        using var document = JsonDocument.Parse("""{"type":"object","required":["path"],"properties":{"path":{"type":"string"}}}""");
        var baseDescriptor = ToolCaptureTestData.Descriptor();
        return new ToolDescriptor(
            baseDescriptor.Id,
            baseDescriptor.Version,
            baseDescriptor.Name,
            baseDescriptor.Description,
            new JsonSchema(baseDescriptor.InputSchema.Dialect, document.RootElement),
            baseDescriptor.OutputSchema,
            baseDescriptor.Effects,
            baseDescriptor.ExecutionHints,
            baseDescriptor.SourceId,
            baseDescriptor.Extensions);
    }

    private static ToolExecutionCapability ExecutionCapability(params ToolExecutionPolicyReference[] policies) =>
        CapabilityOver(budgetScope: null, policies);

    private static ToolExecutionCapability BudgetedCapability(RecordingBudgetScope scope) =>
        CapabilityOver(scope, []);

    private static ToolExecutionCapability CapabilityOver(RecordingBudgetScope? budgetScope, ToolExecutionPolicyReference[] policies) => new(
        new SessionExecutionCapability(
            TestSecurityEvidence.SessionProfile(),
            new UnsupportedSessionCoordinator(),
            new UnsupportedSessionRunCoordinator()),
        budgetScope is null
            ? null
            : new BudgetExecutionCapability(
                new BudgetProfileKey("standard"),
                new BudgetProfileVersion(1),
                TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human),
                new InRunOperationCorrelation(
                    new OperationId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
                    new RunId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                    new TurnId(Guid.Parse("55555555-5555-5555-5555-555555555555"))),
                budgetScope),
        Target,
        [.. (policies.Length == 0 ? [Standard] : policies).Select(static reference => new ToolExecutionPolicyBinding(reference))]);

    private static ToolCallSessionTarget Target { get; } = new(
        new BranchId(Guid.Parse("77777777-7777-7777-7777-777777777771")),
        new ExecutionLaneId(Guid.Parse("77777777-7777-7777-7777-777777777772")));

    private static ToolInvocationResult SuccessResult(string text = "") => new(
        new ToolCallOutcome(
            ToolCallOutcomeKind.Success,
            ToolTerminalStatus.Succeeded,
            SideEffectCertainty.DefinitelyPerformed,
            false,
            null,
            ExtensionData.Empty),
        string.IsNullOrEmpty(text) ? [] : [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)]);

    private sealed class RecordingInvoker(Func<ToolInvocationContext, CancellationToken, ValueTask<ToolInvocationResult>> handler): IToolInvoker
    {
        public int Invocations { get; private set; }

        public async ValueTask<ToolInvocationResult> InvokeAsync(ToolInvocationContext context, CancellationToken cancellationToken = default)
        {
            Invocations++;
            return await handler(context, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class FixedSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
    {
        public SecurityRequestId Create() => new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    }

    private sealed class AllowInvocationAuthority: ISecurityAuthority
    {
        public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            var authorization = request.Authorization
                ?? throw new InvalidOperationException("Tests must supply authorization on security requests.");
            var grant = new SecurityGrant(
                new GrantId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
                request.Id,
                request.Scope,
                request.Identity,
                authorization,
                request.Audience,
                request.Kind,
                request.Effect,
                request.Resources,
                request.InputFingerprint,
                authorization.PolicySnapshot.Version,
                new SecurityRevocationVersion(1),
                DateTimeOffset.UnixEpoch,
                request.Deadline,
                request.RequestedUses);
            return ValueTask.FromResult<SecurityDecision>(
                new SecurityAllowed(request.Id, authorization.PolicySnapshot.Version, grant));
        }
    }

    private sealed class DenyInvocationAuthority: ISecurityAuthority
    {
        public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var authorization = request.Authorization;
            return ValueTask.FromResult<SecurityDecision>(
                new SecurityDenied(
                    request.Id,
                    authorization.PolicySnapshot.Version,
                    new SecurityDenial("test.denied", "Denied by test authority.")));
        }
    }

    private sealed class ApprovalRequiredInvocationAuthority: ISecurityAuthority
    {
        public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var version = request.Authorization.PolicySnapshot.Version;
            var approval = new ApprovalRequest(
                new ApprovalRequestId(Guid.Parse("41000000-0000-0000-0000-000000000004")),
                new ApprovalScopeBinding(
                    request,
                    version,
                    new SecurityRevocationVersion(1),
                    DateTimeOffset.UnixEpoch,
                    DateTimeOffset.UnixEpoch.AddMinutes(5),
                    1),
                "Approve a bounded test operation.",
                DateTimeOffset.UnixEpoch);
            return ValueTask.FromResult<SecurityDecision>(new SecurityApprovalRequired(request.Id, version, approval));
        }
    }

    private sealed class RecordingApprovalWaitRecorder: IApprovalWaitRecorder
    {
        private readonly List<(SecurityRequest Request, ApprovalRequest Approval)> _waits = [];

        public IReadOnlyList<(SecurityRequest Request, ApprovalRequest Approval)> Waits => _waits;

        public ValueTask RecordAsync(SecurityRequest request, ApprovalRequest approval, CancellationToken cancellationToken = default)
        {
            _waits.Add((request, approval));
            return ValueTask.CompletedTask;
        }
    }
}
