// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Parsing;

using AgentKit.Providers.OpenAICompatible.Tests.Fakes;

/// <summary>Verifies OpenAIChatCompletionResponseParser behavior and contracts.</summary>
public sealed class OpenAIChatCompletionResponseParserTests
{
    private static OpenAIResponseParseContext CreateContext(ModelRequestId requestId, ProviderRequestId? providerRequestId = null) => new(requestId, new ProviderId("openai"), new ApiFamilyId("openai-chat-completions"), new ModelId("gpt-4o"), deploymentId: null, providerRequestId);
    [Fact]
    public async Task ParseBufferedAsync_WhenPlainTextResponse_EmitsTextPartAndCompletes()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_success.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.Completed);
        completed.Response.Parts.Length.ShouldBe(1);
        var textPart = completed.Response.Parts[0].ShouldBeOfType<TextPart>();
        textPart.Text.ShouldBe("Hello! How can I help you today?");
        completed.Response.Usage.ReportState.ShouldBe(ModelUsageReportState.Final);
        completed.Response.Usage.InputTokens.ShouldBe(20);
        completed.Response.Usage.OutputTokens.ShouldBe(9);
        completed.Response.Usage.CachedInputTokens.ShouldBe(0);
        completed.Response.Usage.ReasoningTokens.ShouldBe(0);
        completed.Response.Identity.ResolvedModelId.ShouldBe(new ModelId("gpt-4o-2024-08-06"));
        completed.Response.Identity.ResponseId.ShouldBe(new ProviderResponseId("chatcmpl-abc123"));
        _ = observer.Events[0].ShouldBeOfType<ModelResponseStarted>();
        _ = observer.Events.OfType<ModelPartStarted>().ShouldHaveSingleItem();
        _ = observer.Events.OfType<ModelPartCompleted>().ShouldHaveSingleItem();
        _ = observer.Events.OfType<ModelUsageUpdated>().ShouldHaveSingleItem();
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseCompleted>();
        // Sequence numbers must be contiguous and strictly increasing.
        observer.Events.Select(e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(i => (long) i));
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenToolCallResponse_EmitsToolCallPartWithParsedArguments()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_tool_call.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.ToolUse);
        completed.Response.Parts.Length.ShouldBe(1);
        var toolCall = completed.Response.Parts[0].ShouldBeOfType<ToolCallPart>();
        toolCall.Tool.Name.ShouldBe("get_weather");
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("call_xyz789"));
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
        var deltaEvent = observer.Events.OfType<ModelPartDelta>().ShouldHaveSingleItem();
        var argumentsDelta = deltaEvent.Delta.ShouldBeOfType<ToolArgumentsContentDelta>();
        argumentsDelta.ToolCallId.ShouldBe(toolCall.CallId);
        argumentsDelta.JsonFragment.ShouldBe( /*lang=json,strict*/"""{"location":"Paris"}""");
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenParallelToolCalls_EmitsOneToolCallPartPerCallInOrder()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_parallel_tool_calls.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.Length.ShouldBe(2);
        var first = completed.Response.Parts[0].ShouldBeOfType<ToolCallPart>();
        first.Tool.Name.ShouldBe("get_weather");
        first.ProviderCallId.ShouldBe(new ProviderToolCallId("call_alpha"));
        var second = completed.Response.Parts[1].ShouldBeOfType<ToolCallPart>();
        second.Tool.Name.ShouldBe("get_time");
        second.ProviderCallId.ShouldBe(new ProviderToolCallId("call_beta"));
        first.CallId.ShouldNotBe(second.CallId);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenToolArgumentsAreMalformed_ReturnsCorrelatedProtocolFailure()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var providerRequestId = new ProviderRequestId("provider-request-buffered");
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_malformed_tool_arguments.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId, providerRequestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.RequestId.ShouldBe(providerRequestId);
        failed.Failure.DiagnosticCause.ShouldBeNull();
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("partial text");
        failed.Usage.ShouldNotBeNull().InputTokens.ShouldBe(30);
        var argumentDelta = observer.Events.OfType<ModelPartDelta>().Select(responseEvent => responseEvent.Delta).OfType<ToolArgumentsContentDelta>().ShouldHaveSingleItem();
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
        terminal.RequestId.ShouldBe(requestId);
        terminal.Failure.ShouldBe(failed.Failure);
        terminal.PartialParts.ShouldBe(failed.PartialParts);
        observer.Events.Select(responseEvent => responseEvent.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(index => (long) index));
        argumentDelta.ToolCallId.ShouldNotBe(default);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenUsageIsAbsent_ReportsEmptyUsageWithoutEmittingUsageEvent()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_no_usage.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Usage.ShouldBeSameAs(ModelUsage.NotReported);
        observer.Events.OfType<ModelUsageUpdated>().ShouldBeEmpty();
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenUsageIsNegative_ReturnsProtocolFailureWithoutSuccess()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        const string json = /*lang=json,strict*/ """
            {"id":"response","model":"model","choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],"usage":{"prompt_tokens":-1,"completion_tokens":1,"total_tokens":0}}
            """;
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<ModelAttemptFailed>().Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        observer.Events.OfType<ModelResponseCompleted>().ShouldBeEmpty();
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenResponseHasNoChoices_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_no_choices.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenBodyIsNotJson_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        await using var body = new MemoryStream("not json"u8.ToArray());
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<JsonException>();
    }

    public static TheoryData<int> ChunkSizes => [1, 2, 3, 7, 64, 4096];

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenPlainTextStream_EmitsTextDeltasRegardlessOfFragmentation(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_success.sse");
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
    public async Task ParseStreamingAsync_WhenMultibyteUtf8SplitAcrossChunks_ReassemblesText(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_multibyte.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.Single().ShouldBeOfType<TextPart>().Text.ShouldBe("héllo 👋 世界 — ok ✅");
        var textDeltas = observer.Events.OfType<ModelPartDelta>().Select(e => e.Delta).OfType<TextContentDelta>().Select(d => d.Text).ToArray();
        string.Concat(textDeltas).ShouldBe("héllo 👋 世界 — ok ✅");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenCrLfLineEndingsAndKeepaliveComments_ParsesEveryEvent()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        await using var stream = new ChunkedStream(TestResources.ReadAllBytes("responses/streaming_crlf_keepalive.sse"), 3);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<ModelAttemptCompleted>().Response.Parts.Single().ShouldBeOfType<TextPart>().Text.ShouldBe("Hi!");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenToolCallDeltasLackIndex_DoesNotMergeDistinctCalls()
    {
        // Several OpenAI-compatible servers omit `index`; two calls with distinct ids must never merge into one.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        await using var stream = new ChunkedStream(TestResources.ReadAllBytes("responses/streaming_tool_calls_without_index.sse"), 4096);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        if (result is ModelAttemptCompleted completed)
        {
            // Either both calls survive intact, or the parser rejects the ambiguity; a merged/corrupted single call is a silent effect change.
            completed.Response.Parts.OfType<ToolCallPart>().Count().ShouldBe(2);
            completed.Response.Parts.OfType<ToolCallPart>().Select(static part => part.ProviderCallId?.Value).ShouldBe(["call_a", "call_b"]);
        }
        else
        {
            _ = result.ShouldBeOfType<ModelAttemptFailed>();
        }
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenChunkContainsErrorObject_ReturnsProviderFailureRetainingCode()
    {
        // error-taxonomy.md: preserve external status/code; an in-stream error frame is a provider failure, not "stream ended early".
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        await using var stream = new ChunkedStream(TestResources.ReadAllBytes("responses/streaming_error_frame.sse"), 4096);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldNotBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.ProviderCode.ShouldBe("rate_limit_exceeded");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenDeltaCarriesReasoningContent_DoesNotDiscardIt()
    {
        // model-providers-and-capabilities.md: unknown/provider-specific fields are preserved, not discarded.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        await using var stream = new ChunkedStream(TestResources.ReadAllBytes("responses/streaming_reasoning_content.sse"), 4096);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var retained = completed.Response.Parts.Any(static part => part is ReasoningPart)
            || completed.Response.Extensions.Values.Count > 0
            || observer.Events.OfType<ModelPartDelta>().Any(static e => e.Delta is ReasoningContentDelta);
        retained.ShouldBeTrue("reasoning_content was silently dropped");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenToolCallStream_AccumulatesFragmentedArgumentsRegardlessOfFragmentation(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_tool_call.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.ToolUse);
        completed.Response.Parts.Length.ShouldBe(1);
        var toolCall = completed.Response.Parts[0].ShouldBeOfType<ToolCallPart>();
        toolCall.Tool.Name.ShouldBe("get_weather");
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("call_stream1"));
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
        var argumentFragments = observer.Events.OfType<ModelPartDelta>().Select(e => e.Delta).OfType<ToolArgumentsContentDelta>().Select(d => d.JsonFragment).ToArray();
        string.Concat(argumentFragments).ShouldBe( /*lang=json,strict*/"""{"location":"Paris"}""");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenParallelToolCallStream_AccumulatesEachCallIndependently(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_parallel_tool_calls.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.Length.ShouldBe(2);
        var first = completed.Response.Parts[0].ShouldBeOfType<ToolCallPart>();
        first.Tool.Name.ShouldBe("get_weather");
        first.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
        var second = completed.Response.Parts[1].ShouldBeOfType<ToolCallPart>();
        second.Tool.Name.ShouldBe("get_time");
        second.Arguments.GetProperty("timezone").GetString().ShouldBe("UTC");
        first.CallId.ShouldNotBe(second.CallId);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenStreamTruncatedBeforeFinishReason_FailsWithProtocolViolation(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_truncated.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        // A truncated stream must never present itself as a successful completion.
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenChunkIsMalformedJson_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = "data: { not valid json\n\ndata: [DONE]\n"u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenFinalUsageArrivesWithoutDone_RetainsFinalUsage()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_success.sse");
        var withoutDone = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(payload).Replace("data: [DONE]", string.Empty, StringComparison.Ordinal));
        await using var stream = new MemoryStream(withoutDone);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<ModelAttemptCompleted>().Response.Usage.ReportState.ShouldBe(ModelUsageReportState.Final);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenMalformedChunkFollowsFinalUsage_RetainsFinalUsageOnFailure()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_success.sse");
        var malformedTail = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(payload).Replace("data: [DONE]", "data: { malformed", StringComparison.Ordinal));
        await using var stream = new MemoryStream(malformedTail);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Usage.ShouldNotBeNull().ReportState.ShouldBe(ModelUsageReportState.Final);
        observer.Events.OfType<ModelResponseCompleted>().ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenToolArgumentsAreMalformed_ReturnsCorrelatedProtocolFailure(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var providerRequestId = new ProviderRequestId("provider-request-streaming");
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_malformed_tool_arguments.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId, providerRequestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.RequestId.ShouldBe(providerRequestId);
        failed.Failure.DiagnosticCause.ShouldBeNull();
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("partial text");
        var argumentDeltas = observer.Events.OfType<ModelPartDelta>().Select(responseEvent => responseEvent.Delta).OfType<ToolArgumentsContentDelta>().ToArray();
        argumentDeltas.ShouldNotBeEmpty();
        _ = argumentDeltas.Select(delta => delta.ToolCallId).Distinct().ShouldHaveSingleItem();
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
        terminal.RequestId.ShouldBe(requestId);
        terminal.Failure.ShouldBe(failed.Failure);
        terminal.PartialParts.ShouldBe(failed.PartialParts);
        observer.Events.Select(responseEvent => responseEvent.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(index => (long) index));
    }
}
