// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Tests.Parsing;

using System.Text;

using AgentKit.Providers.Anthropic.Tests.Fakes;

/// <summary>Verifies AnthropicMessageStreamParser behavior and contracts.</summary>
public sealed class AnthropicMessageStreamParserTests
{
    private static AnthropicResponseParseContext CreateContext(ModelRequestId requestId) => new(requestId, new ProviderId("anthropic"), new ApiFamilyId("anthropic-messages"), new ModelId("claude-sonnet-4-5"), deploymentId: null, providerRequestId: null);
    [Fact]
    public async Task ParseBufferedAsync_WhenPlainTextResponse_EmitsTextPartAndCompletes()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_text.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.Completed);
        completed.Response.Parts.Length.ShouldBe(1);
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello! How can I help you today?");
        completed.Response.Usage.ReportState.ShouldBe(ModelUsageReportState.Final);
        completed.Response.Usage.InputTokens.ShouldBe(20);
        completed.Response.Usage.OutputTokens.ShouldBe(9);
        completed.Response.Usage.CachedInputTokens.ShouldBe(0);
        completed.Response.Identity.ResolvedModelId.ShouldBe(new ModelId("claude-sonnet-4-5-20250929"));
        completed.Response.Identity.ResponseId.ShouldBe(new ProviderResponseId("msg_01abc"));
        _ = observer.Events[0].ShouldBeOfType<ModelResponseStarted>();
        _ = observer.Events.OfType<ModelPartStarted>().ShouldHaveSingleItem();
        _ = observer.Events.OfType<ModelPartCompleted>().ShouldHaveSingleItem();
        _ = observer.Events.OfType<ModelUsageUpdated>().ShouldHaveSingleItem();
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseCompleted>();
        observer.Events.Select(e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(i => (long) i));
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenToolUseResponse_EmitsToolCallPartWithStructuredArguments()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_tool_use.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.ToolUse);
        completed.Response.Parts.Length.ShouldBe(1);
        var toolCall = completed.Response.Parts[0].ShouldBeOfType<ToolCallPart>();
        toolCall.Tool.Name.ShouldBe("get_weather");
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("toolu_01xyz"));
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
        var deltaEvent = observer.Events.OfType<ModelPartDelta>().ShouldHaveSingleItem();
        var argumentsDelta = deltaEvent.Delta.ShouldBeOfType<ToolArgumentsContentDelta>();
        argumentsDelta.ToolCallId.ShouldBe(toolCall.CallId);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenThinkingAndTextResponse_EmitsReasoningThenTextParts()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_thinking.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.Length.ShouldBe(2);
        var reasoning = completed.Response.Parts[0].ShouldBeOfType<ReasoningPart>();
        reasoning.Content.Visibility.ShouldBe(ReasoningVisibility.Visible);
        reasoning.Content.Text.ShouldBe("Let me consider this carefully...");
        reasoning.Content.SignatureToken.ShouldBe("sig_abc123");
        completed.Response.Parts[1].ShouldBeOfType<TextPart>().Text.ShouldBe("The answer is 42.");
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenUsageTokenCountIsNegative_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/buffered_text.json").Replace("\"input_tokens\": 20", "\"input_tokens\": -1", StringComparison.Ordinal);
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        observer.Events.ShouldNotContain(@event => @event is ModelResponseCompleted);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenBodyIsNotJson_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        await using var body = new MemoryStream("not json"u8.ToArray());
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<JsonException>();
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenToolArgumentsAreTruncatedByMaxTokens_ReturnsTypedFailureWithoutThrowing()
    {
        // Anthropic still sends content_block_stop after max_tokens truncates partial_json; the parser must return a typed failure.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        await using var stream = new ChunkedStream(TestResources.ReadAllBytes("responses/streaming_tool_use_truncated_arguments.sse"), 4096);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<ModelAttemptFailed>();
        observer.Events.OfType<ModelResponseFailed>().Count().ShouldBe(1);
    }

    public static TheoryData<int> ChunkSizes => [1, 2, 3, 7, 64, 4096];

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenPlainTextStream_EmitsTextDeltasRegardlessOfFragmentation(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_text.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.Completed);
        completed.Response.Parts.Length.ShouldBe(1);
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");
        completed.Response.Usage.ReportState.ShouldBe(ModelUsageReportState.Final);
        completed.Response.Usage.InputTokens.ShouldBe(10);
        completed.Response.Usage.OutputTokens.ShouldBe(2);
        var textDeltas = observer.Events.OfType<ModelPartDelta>().Select(e => e.Delta).OfType<TextContentDelta>().Select(d => d.Text).ToArray();
        textDeltas.ShouldBe(["Hello", "!"]);
        observer.Events.Select(e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(i => (long) i));
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenToolUseStream_AccumulatesFragmentedArgumentsRegardlessOfFragmentation(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_tool_use.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.ToolUse);
        completed.Response.Parts.Length.ShouldBe(1);
        var toolCall = completed.Response.Parts[0].ShouldBeOfType<ToolCallPart>();
        toolCall.Tool.Name.ShouldBe("get_weather");
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("toolu_01xyz"));
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
        var argumentFragments = observer.Events.OfType<ModelPartDelta>().Select(e => e.Delta).OfType<ToolArgumentsContentDelta>().Select(d => d.JsonFragment).ToArray();
        string.Concat(argumentFragments).ShouldBe( /*lang=json,strict*/"""{"location":"Paris"}""");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenThinkingThenTextStream_EmitsReasoningThenTextPartsInOrder(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_thinking.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.Length.ShouldBe(2);
        var reasoning = completed.Response.Parts[0].ShouldBeOfType<ReasoningPart>();
        reasoning.Content.Text.ShouldBe("Let me consider...");
        reasoning.Content.SignatureToken.ShouldBe("sig_abc123");
        completed.Response.Parts[1].ShouldBeOfType<TextPart>().Text.ShouldBe("The answer is 42.");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenStreamTruncatedBeforeMessageStop_FailsWithProtocolViolation(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_truncated.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Usage.ShouldNotBeNull().ReportState.ShouldBe(ModelUsageReportState.Interim);
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenMessageStopIsMissingAfterFinalUsage_RetainsFinalUsageOnFailure()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_text.sse");
        var withoutStop = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(payload).Replace("event: message_stop\ndata: {\"type\":\"message_stop\"}", string.Empty, StringComparison.Ordinal));
        await using var stream = new MemoryStream(withoutStop);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Usage.ShouldNotBeNull().ReportState.ShouldBe(ModelUsageReportState.Final);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenStopReasonDoesNotCarryUsage_DoesNotPromoteInitialUsage()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = /*lang=text*/ """
            event: message_start
            data: {"type":"message_start","message":{"id":"message","model":"claude","content":[],"usage":{"input_tokens":10,"output_tokens":0}}}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"}}

            event: message_stop
            data: {"type":"message_stop"}

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Usage.ReportState.ShouldBe(ModelUsageReportState.Interim);
        completed.Response.Usage.InputTokens.ShouldBe(10);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenErrorEventArrivesMidStream_FailsWithMappedProviderFailureKind()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_error_event.sse");
        await using var stream = new ChunkedStream(payload, 64);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failed.Failure.SafeMessage.ShouldBe("Overloaded");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenChunkIsMalformedJson_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = "event: content_block_delta\ndata: { not valid json\n\n"u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }
}
