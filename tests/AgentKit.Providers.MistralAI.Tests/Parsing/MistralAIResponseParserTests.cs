// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests.Parsing;

using System.Text;

using AgentKit.Providers.MistralAI.Tests.Fakes;
using AgentKit.TestSupport;

/// <summary>Verifies MistralAIResponseParser behavior and contracts.</summary>
public sealed class MistralAIResponseParserTests
{
    public static TheoryData<int> ChunkSizes => [1, 2, 3, 7, 64, 4096];

    private static ProviderResponseParseContext CreateContext(ModelRequestId requestId) => new(requestId, MistralAIProviderDefaults.ProviderId, MistralAIProviderDefaults.ApiFamily, new ModelId("mistral-large-latest"), deploymentId: null, providerRequestId: null);
    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenPlainTextStream_EmitsTextDeltasRegardlessOfFragmentation(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
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
    public async Task ParseStreamingAsync_WhenCrLfLineEndingsKeepaliveCommentsAndByteOrderMark_ParsesEveryEvent(int chunkSize)
    {
        // The shared server-sent-event reader owns line endings, comments, and the BOM; this proves the parser is
        // wired through it rather than through a private data-line loop.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var text = Encoding.UTF8.GetString(TestResources.ReadAllBytes("responses/streaming_text.sse"));
        var hostile = ": keepalive\n\n" + text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal).Replace("\r\n\r\n", "\r\n: keepalive\r\n\r\n", StringComparison.Ordinal);
        var payload = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(hostile)).ToArray();
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.Completed);
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");
        var textDeltas = observer.Events.OfType<ModelPartDelta>().Select(e => e.Delta).OfType<TextContentDelta>().Select(d => d.Text).ToArray();
        textDeltas.ShouldBe(["Hello", "!"]);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenToolArgumentsAreMalformed_ReturnsProtocolFailureInsteadOfEmptyArguments()
    {
        // streaming-and-event-protocol.md: a malformed stream "MUST NOT synthesize success"; `{}` is a fabricated call.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_malformed_tool_arguments.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<ModelAttemptFailed>().Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenOnlyNonterminalChunkCarriesUsage_RetainsInterimUsage()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = /*lang=text*/ """
            data: {"id":"cmpl-s02","model":"mistral-large-latest-2412","choices":[{"index":0,"delta":{"role":"assistant","content":"Hello"},"finish_reason":null}],"usage":{"prompt_tokens":10,"completion_tokens":null,"total_tokens":10}}

            data: {"id":"cmpl-s02","model":"mistral-large-latest-2412","choices":[{"index":0,"delta":{"content":"!"},"finish_reason":"stop"}]}

            data: [DONE]

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Usage.ReportState.ShouldBe(ModelUsageReportState.Interim);
        completed.Response.Usage.InputTokens.ShouldBe(10);
        completed.Response.Usage.OutputTokens.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenToolUseStream_AccumulatesFragmentedArgumentsRegardlessOfFragmentation(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_tool_use.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.ToolUse);
        completed.Response.Parts.Length.ShouldBe(1);
        var toolCall = completed.Response.Parts[0].ShouldBeOfType<ToolCallPart>();
        toolCall.Tool.ProviderAlias.Value.ShouldBe("get_weather");
        toolCall.Tool.Id.ShouldBeNull();
        toolCall.Tool.IsResolved.ShouldBeFalse();
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("call_stream01"));
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
        var argumentFragments = observer.Events.OfType<ModelPartDelta>().Select(e => e.Delta).OfType<ToolArgumentsContentDelta>().Select(d => d.JsonFragment).ToArray();
        string.Concat(argumentFragments).ShouldBe( /*lang=json,strict*/"""{"location":"Paris"}""");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenParallelToolCallsStream_CorrelatesFragmentsByWireIndex(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_parallel_tool_calls.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.Length.ShouldBe(2);
        var first = completed.Response.Parts[0].ShouldBeOfType<ToolCallPart>();
        first.Tool.ProviderAlias.Value.ShouldBe("get_weather");
        first.ProviderCallId.ShouldBe(new ProviderToolCallId("call_a"));
        first.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
        var second = completed.Response.Parts[1].ShouldBeOfType<ToolCallPart>();
        second.Tool.ProviderAlias.Value.ShouldBe("get_time");
        second.ProviderCallId.ShouldBe(new ProviderToolCallId("call_b"));
        second.Arguments.GetProperty("zone").GetString().ShouldBe("UTC");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenUnknownContentChunk_WrapsAsUnknownContentPart(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_unknown_chunk.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var unknown = completed.Response.Parts[0].ShouldBeOfType<UnknownContentPart>();
        unknown.TypeName.ShouldBe("image_url");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenStreamEndsWithoutDoneSentinel_FailsWithProtocolViolation(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_truncated.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        // The open text part is materialized as far as it was received, never dropped.
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello");
        observer.Events[^1].ShouldBeOfType<ModelResponseFailed>().PartialParts.ShouldBe(failed.PartialParts);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenToolCallChunksNeverCarryFunctionName_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"id": "cmpl-noname", "model": "mistral-large-latest-2412", "choices": [{"index": 0, "delta": {"role": "assistant", "tool_calls": [{"index": 0, "id": "call_noname", "type": "function", "function": {"arguments": "{\"location\":"}}]}, "finish_reason": null}]}

            data: {"id": "cmpl-noname", "model": "mistral-large-latest-2412", "choices": [{"index": 0, "delta": {"tool_calls": [{"index": 0, "function": {"arguments": "\"Paris\"}"}}]}, "finish_reason": "tool_calls"}]}

            data: [DONE]


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.OfType<ToolCallPart>().ShouldBeEmpty();
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenChunkIsMalformedJson_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = "data: { not valid json\n\n"u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenPlainTextResponse_EmitsTextPartAndCompletes()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_text.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.Completed);
        completed.Response.Parts.Length.ShouldBe(1);
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello! How can I help you today?");
        completed.Response.Usage.ReportState.ShouldBe(ModelUsageReportState.Final);
        completed.Response.Usage.InputTokens.ShouldBe(20);
        completed.Response.Usage.OutputTokens.ShouldBe(9);
        completed.Response.Usage.CachedInputTokens.ShouldBeNull();
        completed.Response.Identity.ResolvedModelId.ShouldBe(new ModelId("mistral-large-latest-2412"));
        completed.Response.Identity.ResponseId.ShouldBe(new ProviderResponseId("cmpl-e5cc70bb28c444948073e77776eb30ef"));
        _ = observer.Events[0].ShouldBeOfType<ModelResponseStarted>();
        _ = observer.Events.OfType<ModelPartStarted>().ShouldHaveSingleItem();
        _ = observer.Events.OfType<ModelPartCompleted>().ShouldHaveSingleItem();
        _ = observer.Events.OfType<ModelUsageUpdated>().ShouldHaveSingleItem();
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseCompleted>();
        observer.Events.Select(e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(i => (long) i));
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenToolUseResponse_EmitsToolCallPartWithToolUseStopReason()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_tool_use.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.ToolUse);
        completed.Response.Parts.Length.ShouldBe(1);
        var toolCall = completed.Response.Parts[0].ShouldBeOfType<ToolCallPart>();
        toolCall.Tool.ProviderAlias.Value.ShouldBe("get_weather");
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("call_xyz789"));
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
        var deltaEvent = observer.Events.OfType<ModelPartDelta>().ShouldHaveSingleItem();
        var argumentsDelta = deltaEvent.Delta.ShouldBeOfType<ToolArgumentsContentDelta>();
        argumentsDelta.ToolCallId.ShouldBe(toolCall.CallId);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenMixedContentAndToolCall_OrdersContentPartsBeforeToolCalls()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_mixed_content_and_tool_call.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.Length.ShouldBe(2);
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Let me check that for you.");
        completed.Response.Parts[1].ShouldBeOfType<ToolCallPart>().Tool.ProviderAlias.Value.ShouldBe("get_weather");
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenUnknownContentChunk_WrapsAsUnknownContentPart()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_unknown_chunk.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var unknown = completed.Response.Parts[0].ShouldBeOfType<UnknownContentPart>();
        unknown.TypeName.ShouldBe("image_url");
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenNoChoices_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/no_choices.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenMoreThanOneChoiceIsReturned_FailsWithProtocolViolationInsteadOfDiscardingIt()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_multiple_choices.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenAChunkStreamsASecondChoiceIndex_FailsWithProtocolViolationInsteadOfMergingIt()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_multiple_choices.sse");
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        // The choice-0 delta already observed must still be reported, not silently dropped.
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello");
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenUsageTokenCountIsNegative_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/buffered_text.json").Replace("\"prompt_tokens\": 20", "\"prompt_tokens\": -1", StringComparison.Ordinal);
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        observer.Events.ShouldNotContain(@event => @event is ModelResponseCompleted);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenToolCallArgumentsAreJsonObject_UsesObjectDirectly()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/buffered_tool_use.json")
            .Replace("\"arguments\": \"{\\\"location\\\": \\\"Paris\\\"}\"", "\"arguments\": {\"location\": \"Paris\"}", StringComparison.Ordinal);
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var toolCall = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolCallPart>();
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
    }

    /// <summary>Verifies a tool call whose arguments field is present but neither a string nor an object (e.g. null) is treated as an empty-object argument rather than throwing.</summary>
    [Fact]
    public async Task ParseBufferedAsync_WhenToolCallArgumentsFieldIsNull_TreatsAsEmptyObject()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/buffered_tool_use.json")
            .Replace("\"arguments\": \"{\\\"location\\\": \\\"Paris\\\"}\"", "\"arguments\": null", StringComparison.Ordinal);
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var toolCall = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolCallPart>();
        toolCall.Arguments.GetRawText().ShouldBe("{}");
    }

    /// <summary>Verifies every documented Mistral finish reason maps to its normalized stop reason, including unmapped and absent values.</summary>
    [Theory]
    [InlineData("length", NormalizedStopReason.Length)]
    [InlineData("model_length", NormalizedStopReason.Length)]
    [InlineData("error", NormalizedStopReason.Error)]
    [InlineData("some_future_reason", NormalizedStopReason.Error)]
    public async Task ParseBufferedAsync_WhenFinishReasonVaries_MapsToExpectedNormalizedStopReason(string finishReason, NormalizedStopReason expected)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/buffered_text.json")
            .Replace("\"finish_reason\": \"stop\"", $"\"finish_reason\": \"{finishReason}\"", StringComparison.Ordinal);
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(expected);
    }

    /// <summary>Verifies a response with no finish_reason at all is normalized as still pending rather than completed.</summary>
    [Fact]
    public async Task ParseBufferedAsync_WhenFinishReasonIsAbsent_MapsToPending()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/buffered_text.json")
            .Replace("\"finish_reason\": \"stop\"", "\"finish_reason\": null", StringComparison.Ordinal);
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.Pending);
    }

    /// <summary>Verifies a tool-call-delta whose function.arguments is a JSON object (not a string) is accumulated via its raw text.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenToolCallArgumentsFragmentIsJsonObject_AppendsRawObjectText()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"id": "cmpl-objargs", "model": "mistral-large-latest-2412", "choices": [{"index": 0, "delta": {"role": "assistant", "tool_calls": [{"index": 0, "id": "call_obj", "type": "function", "function": {"name": "get_weather", "arguments": {"location": "Paris"}}}]}, "finish_reason": null}]}

            data: {"id": "cmpl-objargs", "model": "mistral-large-latest-2412", "choices": [{"index": 0, "delta": {}, "finish_reason": "tool_calls"}]}

            data: [DONE]


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var toolCall = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolCallPart>();
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
    }

    /// <summary>Verifies a tool-call-delta whose arguments fragment is neither a string nor an object (e.g. null) contributes no fragment rather than throwing.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenToolCallArgumentsFragmentIsNull_ContributesNoFragment()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"id": "cmpl-nullargs", "model": "mistral-large-latest-2412", "choices": [{"index": 0, "delta": {"role": "assistant", "tool_calls": [{"index": 0, "id": "call_null", "type": "function", "function": {"name": "get_weather", "arguments": null}}]}, "finish_reason": null}]}

            data: {"id": "cmpl-nullargs", "model": "mistral-large-latest-2412", "choices": [{"index": 0, "delta": {}, "finish_reason": "tool_calls"}]}

            data: [DONE]


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var toolCall = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolCallPart>();
        toolCall.Arguments.GetRawText().ShouldBe("{}");
        observer.Events.OfType<ModelPartDelta>().ShouldBeEmpty();
    }

    /// <summary>Verifies an empty text-content delta still opens the text slot but contributes no delta event or text.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenTextContentFragmentIsEmpty_OpensSlotWithoutEmittingDelta()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"id": "cmpl-emptytext", "model": "mistral-large-latest-2412", "choices": [{"index": 0, "delta": {"role": "assistant", "content": ""}, "finish_reason": null}]}

            data: {"id": "cmpl-emptytext", "model": "mistral-large-latest-2412", "choices": [{"index": 0, "delta": {"content": "Hi"}, "finish_reason": "stop"}]}

            data: [DONE]


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hi");
        var textDeltas = observer.Events.OfType<ModelPartDelta>().Select(e => e.Delta).OfType<TextContentDelta>().Select(d => d.Text).ToArray();
        textDeltas.ShouldBe(["Hi"]);
    }

    /// <summary>Verifies a truncated stream that already closed an unknown-kind slot retains that slot's materialized part in its partial output.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenStreamTruncatedAfterClosedUnknownChunk_RetainsItInPartialParts()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"id": "cmpl-trunc-unknown", "model": "mistral-large-latest-2412", "choices": [{"index": 0, "delta": {"role": "assistant", "content": [{"type": "image_url", "image_url": "https://example.com/x.png"}]}, "finish_reason": null}]}


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        var unknown = failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<UnknownContentPart>();
        unknown.TypeName.ShouldBe("image_url");
    }

    /// <summary>Verifies a truncated stream with an open tool-call slot whose accumulated arguments are malformed JSON omits it from partial parts rather than fabricating a call.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenStreamTruncatedWithMalformedOpenToolCallArguments_OmitsItFromPartialParts()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"id": "cmpl-trunc-badargs", "model": "mistral-large-latest-2412", "choices": [{"index": 0, "delta": {"role": "assistant", "tool_calls": [{"index": 0, "id": "call_bad", "type": "function", "function": {"name": "get_weather", "arguments": "{bad json"}}]}, "finish_reason": null}]}


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.OfType<ToolCallPart>().ShouldBeEmpty();
    }

    /// <summary>Verifies a truncated stream that already received invalid (negative) usage evidence retains no usage rather than propagating that validation failure as the outcome's cause.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenStreamTruncatedAfterNegativeUsage_RetainsNoUsage()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"id": "cmpl-trunc-negusage", "model": "mistral-large-latest-2412", "choices": [{"index": 0, "delta": {"role": "assistant", "content": "Hi"}, "finish_reason": null}], "usage": {"prompt_tokens": -1, "completion_tokens": 1, "total_tokens": 0}}


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Usage.ShouldBeNull();
    }

    /// <summary>Verifies a tool-call slot that never received an explicit close event but has malformed accumulated arguments fails closed at the terminal [DONE] flush.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenToolCallArgumentsAreMalformedAtDoneSentinel_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"id": "cmpl-donebadargs", "model": "mistral-large-latest-2412", "choices": [{"index": 0, "delta": {"role": "assistant", "tool_calls": [{"index": 0, "id": "call_bad", "type": "function", "function": {"name": "get_weather", "arguments": "{bad json"}}]}, "finish_reason": "tool_calls"}]}

            data: [DONE]


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned malformed tool-call arguments.");
        failed.PartialParts.OfType<ToolCallPart>().ShouldBeEmpty();
    }

    /// <summary>Verifies negative final usage evidence at the terminal [DONE] flush fails closed rather than reporting fabricated usage.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenFinalUsageTokenCountIsNegative_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/streaming_text.sse")
            .Replace("\"prompt_tokens\": 10", "\"prompt_tokens\": -1", StringComparison.Ordinal);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenBodyIsNotJson_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new MistralAIResponseParser(new SequentialToolCallIdGenerator());
        await using var body = new MemoryStream("not json"u8.ToArray());
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<JsonException>();
    }
}
