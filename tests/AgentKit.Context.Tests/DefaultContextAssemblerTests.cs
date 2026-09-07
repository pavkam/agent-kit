// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Tests;

public sealed class DefaultContextAssemblerTests
{
    private readonly DefaultContextAssembler _assembler = new();

    [Fact]
    public async Task AssembleAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => _assembler.AssembleAsync(null!));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task AssembleAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var request = TestFactory.AssemblyRequest([TestFactory.UserMessage()]);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => _assembler.AssembleAsync(request, cts.Token));
    }

    [Fact]
    public async Task AssembleAsync_WhenHistoryIsEmpty_ReturnsContextPreparationFailedWithEmptyHistory()
    {
        var request = TestFactory.AssemblyRequest([]);

        var result = await _assembler.AssembleAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ContextPreparationFailed>();
        failed.Failure.Kind.ShouldBe(ContextPreparationFailureKind.EmptyHistory);
    }

    [Fact]
    public async Task AssembleAsync_WhenHistoryHasOnlyIncompleteMessages_ReturnsContextPreparationFailedWithEmptyHistory()
    {
        var request = TestFactory.AssemblyRequest([TestFactory.UserMessage(state: MessageState.Incomplete)]);

        var result = await _assembler.AssembleAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ContextPreparationFailed>();
        failed.Failure.Kind.ShouldBe(ContextPreparationFailureKind.EmptyHistory);
    }

    [Fact]
    public async Task AssembleAsync_WhenAssistantToolCallHasNoMatchingResult_ReturnsBrokenToolCallCausality()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var history = ImmutableArray.Create<AgentMessage>(
            TestFactory.UserMessage(),
            TestFactory.AssistantMessageWithParts([TestFactory.ToolCall(callId)]));

        var request = TestFactory.AssemblyRequest(history);

        var result = await _assembler.AssembleAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ContextPreparationFailed>();
        failed.Failure.Kind.ShouldBe(ContextPreparationFailureKind.BrokenToolCallCausality);
    }

    [Fact]
    public async Task AssembleAsync_WhenToolResultReferencesUnknownCall_ReturnsBrokenToolCallCausality()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var history = ImmutableArray.Create<AgentMessage>(
            TestFactory.UserMessage(),
            TestFactory.ToolMessageWithParts([TestFactory.ToolResult(callId)]));

        var request = TestFactory.AssemblyRequest(history);

        var result = await _assembler.AssembleAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ContextPreparationFailed>();
        failed.Failure.Kind.ShouldBe(ContextPreparationFailureKind.BrokenToolCallCausality);
    }

    [Fact]
    public async Task AssembleAsync_WhenToolCallHasMatchingResult_ReturnsContextReady()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var history = ImmutableArray.Create<AgentMessage>(
            TestFactory.UserMessage(),
            TestFactory.AssistantMessageWithParts([TestFactory.ToolCall(callId)]),
            TestFactory.ToolMessageWithParts([TestFactory.ToolResult(callId)]));

        var request = TestFactory.AssemblyRequest(history);

        var result = await _assembler.AssembleAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ContextReady>();
    }

    [Fact]
    public async Task AssembleAsync_WhenHistoryIsValid_ReturnsContextReadyWithInstructionsFirst()
    {
        var instruction = TestFactory.UserMessage("system prompt");
        var userMessage = TestFactory.UserMessage("hello");
        var request = TestFactory.AssemblyRequest([userMessage], [instruction]);

        var result = await _assembler.AssembleAsync(request, TestContext.Current.CancellationToken);

        var ready = result.ShouldBeOfType<ContextReady>();
        ready.Context.Messages.ShouldBe([instruction, userMessage]);
        ready.Context.ModelRequestId.ShouldBe(request.ModelRequestId);
        ready.Context.Model.ShouldBe(request.Model);
        ready.Context.Tools.ShouldBe(request.Tools);
        ready.Context.ToolChoice.ShouldBe(request.ToolChoice);
        ready.Context.Settings.ShouldBe(request.Settings);
        ready.Context.Extensions.ShouldBe(request.Extensions);
    }

    [Fact]
    public async Task AssembleAsync_WhenHistoryContainsIncompleteMessage_DropsItFromResult()
    {
        var complete = TestFactory.UserMessage("keep");
        var incomplete = TestFactory.UserMessage("drop", MessageState.Incomplete);
        var request = TestFactory.AssemblyRequest([complete, incomplete]);

        var result = await _assembler.AssembleAsync(request, TestContext.Current.CancellationToken);

        var ready = result.ShouldBeOfType<ContextReady>();
        ready.Context.Messages.ShouldBe([complete]);
    }

    [Fact]
    public async Task AssembleAsync_WhenObserved_EmitsCorrelatedContentFreeActivity()
    {
        const string protectedContent = "do-not-export-this-content";
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var request = TestFactory.AssemblyRequest([TestFactory.UserMessage(protectedContent)]);

        _ = await _assembler.AssembleAsync(request, TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.ContextPrepare);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.ModelRequestId).ShouldBe(request.ModelRequestId.ToString());
        activity.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain(protectedContent);
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;
}
