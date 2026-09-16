// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Tests.Parsing;

using System.Text;

using AgentKit.Providers.Anthropic.Tests.Fakes;
using AgentKit.TestSupport;

/// <summary>Verifies AnthropicMessageStreamParser behavior and contracts.</summary>
public sealed class AnthropicMessageStreamParserTests
{
    private static ProviderResponseParseContext CreateContext(ModelRequestId requestId) => new(requestId, new ProviderId("anthropic"), new ApiFamilyId("anthropic-messages"), new ModelId("claude-sonnet-4-5"), deploymentId: null, providerRequestId: null);
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
        toolCall.Tool.ProviderAlias.Value.ShouldBe("get_weather");
        toolCall.Tool.Id.ShouldBeNull();
        toolCall.Tool.IsResolved.ShouldBeFalse();
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
    public async Task ParseBufferedAsync_WhenUsageIsAbsent_ReportsNotReportedUsage()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        const string json = /*lang=json,strict*/ """
            {"id":"msg_no_usage","type":"message","role":"assistant","model":"claude-sonnet-4-5-20250929","content":[{"type":"text","text":"hi"}],"stop_reason":"end_turn","stop_sequence":null}
            """;
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Usage.ShouldBeSameAs(ModelUsage.NotReported);
        observer.Events.OfType<ModelUsageUpdated>().ShouldBeEmpty();
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenContentContainsRedactedThinkingBlock_EmitsRedactedReasoningPart()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        const string json = /*lang=json,strict*/ """
            {"id":"msg_redacted","type":"message","role":"assistant","model":"claude-sonnet-4-5-20250929","content":[{"type":"redacted_thinking","data":"opaque_redacted_payload"}],"stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":5,"output_tokens":1}}
            """;
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var reasoning = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ReasoningPart>();
        reasoning.Content.Visibility.ShouldBe(ReasoningVisibility.Redacted);
        reasoning.Content.SignatureToken.ShouldBe("opaque_redacted_payload");
        observer.Events.OfType<ModelPartDelta>().ShouldBeEmpty();
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenContentBlockKindIsUnrecognized_EmitsUnknownContentPart()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        const string json = /*lang=json,strict*/ """
            {"id":"msg_unknown","type":"message","role":"assistant","model":"claude-sonnet-4-5-20250929","content":[{"type":"server_tool_use","id":"srvtool_1","name":"web_search"}],"stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":5,"output_tokens":1}}
            """;
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var unknown = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<UnknownContentPart>();
        unknown.TypeName.ShouldBe("server_tool_use");
        unknown.Payload.GetProperty("name").GetString().ShouldBe("web_search");
        observer.Events.OfType<ModelPartDelta>().ShouldBeEmpty();
    }

    public static TheoryData<string, NormalizedStopReason> StopReasonMappings => new()
    {
        { "end_turn", NormalizedStopReason.Completed },
        { "stop_sequence", NormalizedStopReason.Completed },
        { "max_tokens", NormalizedStopReason.Length },
        { "tool_use", NormalizedStopReason.ToolUse },
        { "pause_turn", NormalizedStopReason.Deferred },
        { "refusal", NormalizedStopReason.Error },
        { "some_future_stop_reason", NormalizedStopReason.Error },
    };

    [Theory]
    [MemberData(nameof(StopReasonMappings))]
    public async Task ParseBufferedAsync_WhenStopReasonVaries_MapsToExpectedNormalizedStopReason(string stopReason, NormalizedStopReason expected)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var json = "{\"id\":\"msg_stop\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"claude-sonnet-4-5-20250929\",\"content\":[{\"type\":\"text\",\"text\":\"hi\"}],\"stop_reason\":\""
            + stopReason
            + "\",\"stop_sequence\":null,\"usage\":{\"input_tokens\":5,\"output_tokens\":1}}";
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(expected);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenMessageStopArrivesWithoutAPrecedingMessageDelta_ReportsPendingStopReason()
    {
        // No message_delta ever carried a stop_reason, so the response's stop reason falls through to Pending
        // rather than being reported as an arbitrary default.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = /*lang=text*/ """
            event: message_start
            data: {"type":"message_start","message":{"id":"message","model":"claude","content":[],"usage":{"input_tokens":10,"output_tokens":0}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hi"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_stop
            data: {"type":"message_stop"}

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.Pending);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenMessageStopArrivesWithUnclosedContentBlock_ReturnsProtocolFailureWithPartialContent()
    {
        // message_stop can legitimately arrive without every content_block_stop first being observed; the block
        // that never closed is a protocol violation, not a block silently promoted to complete.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = /*lang=text*/ """
            event: message_start
            data: {"type":"message_start","message":{"id":"message","model":"claude","content":[],"usage":{"input_tokens":10,"output_tokens":0}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Partial"}}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":1}}

            event: message_stop
            data: {"type":"message_stop"}

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider's streaming response ended before content block 0 was closed.");
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Partial");
        failed.Usage.ShouldNotBeNull().InputTokens.ShouldBe(10);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenUnclosedContentBlockCoincidesWithInvalidUsage_RetainsNoUsageOnFailure()
    {
        // Invalid usage evidence must never mask the unclosed-block cause reported first; TryBuildRetainedUsage
        // swallows the secondary validation failure and reports no usage instead of throwing.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = /*lang=text*/ """
            event: message_start
            data: {"type":"message_start","message":{"id":"message","model":"claude","content":[],"usage":{"input_tokens":-1,"output_tokens":0}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Partial"}}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":1}}

            event: message_stop
            data: {"type":"message_stop"}

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider's streaming response ended before content block 0 was closed.");
        failed.Usage.ShouldBeNull();
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Partial");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenAllBlocksCloseButFinalUsageIsInvalid_ReturnsProtocolFailure()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = /*lang=text*/ """
            event: message_start
            data: {"type":"message_start","message":{"id":"message","model":"claude","content":[],"usage":{"input_tokens":10,"output_tokens":0}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Done"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":-1}}

            event: message_stop
            data: {"type":"message_stop"}

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned invalid usage evidence.");
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Done");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenStreamEndsWithInvalidUsageEvidence_RetainsNoUsageOnFailure()
    {
        // A negative token count fails ModelUsage's own validation; the interrupted-stream failure path must
        // swallow that secondary validation error rather than letting it mask the real protocol-violation cause.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_text.sse");
        var withNegativeUsage = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(payload).Replace("\"input_tokens\":10", "\"input_tokens\":-1", StringComparison.Ordinal));
        var withoutStop = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(withNegativeUsage).Replace("event: message_stop\ndata: {\"type\":\"message_stop\"}", string.Empty, StringComparison.Ordinal));
        await using var stream = new MemoryStream(withoutStop);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Usage.ShouldBeNull();
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
    public async Task ParseStreamingAsync_WhenCrLfLineEndingsKeepaliveCommentsAndByteOrderMark_ParsesEveryEvent(int chunkSize)
    {
        // The shared server-sent-event reader owns line endings, comments, and the BOM; this proves the parser is
        // wired through it rather than through a private data-line loop.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
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
        toolCall.Tool.ProviderAlias.Value.ShouldBe("get_weather");
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
        // The open text block is materialized as far as it was received, never dropped.
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Partial");
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        observer.Events[^1].ShouldBeOfType<ModelResponseFailed>().PartialParts.ShouldBe(failed.PartialParts);
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

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenToolUseStreamOpensWithEmptyInputJsonDelta_IgnoresItAndParsesIdentically(int chunkSize)
    {
        // Anthropic's documented tool-use stream begins every tool_use block with
        // {"type":"input_json_delta","partial_json":""} (https://docs.anthropic.com/en/docs/build-with-claude/streaming).
        var requestId = new ModelRequestId(Guid.NewGuid());
        var referenceObserver = new RecordingModelResponseObserver();
        await using var referenceStream = new MemoryStream(TestResources.ReadAllBytes("responses/streaming_tool_use.sse"));
        var reference = (await new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator())
            .ParseStreamingAsync(referenceStream, CreateContext(requestId), referenceObserver, TestContext.Current.CancellationToken))
            .ShouldBeOfType<ModelAttemptCompleted>();

        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        await using var stream = new ChunkedStream(TestResources.ReadAllBytes("responses/streaming_tool_use_leading_empty_delta.sse"), chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        observer.Events.OfType<ModelPartDelta>().Select(e => e.Delta).ShouldNotContain(d => d is ProviderContentDelta);
        var toolCall = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolCallPart>();
        var referenceToolCall = reference.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolCallPart>();
        toolCall.CallId.ShouldBe(referenceToolCall.CallId);
        toolCall.Tool.ShouldBe(referenceToolCall.Tool);
        toolCall.ProviderCallId.ShouldBe(referenceToolCall.ProviderCallId);
        toolCall.Arguments.GetRawText().ShouldBe(referenceToolCall.Arguments.GetRawText());
        completed.Response.StopReason.ShouldBe(reference.Response.StopReason);
        completed.Response.Usage.ShouldBe(reference.Response.Usage);
        observer.Events.Select(e => e.GetType()).ShouldBe(referenceObserver.Events.Select(e => e.GetType()));
        var argumentFragments = observer.Events.OfType<ModelPartDelta>().Select(e => e.Delta).OfType<ToolArgumentsContentDelta>().Select(d => d.JsonFragment).ToArray();
        argumentFragments.ShouldBe(referenceObserver.Events.OfType<ModelPartDelta>().Select(e => e.Delta).OfType<ToolArgumentsContentDelta>().Select(d => d.JsonFragment));
        argumentFragments.ShouldNotContain(string.Empty);
        observer.Events.Select(e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(i => (long) i));
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenThinkingDeltaIsEmpty_EmitsNoDeltaAndKeepsSignature()
    {
        // With display omitted, Anthropic opens the thinking block, sends an empty thinking_delta, then a signature_delta.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = /*lang=text*/ """
            event: message_start
            data: {"type":"message_start","message":{"id":"message","model":"claude","content":[],"usage":{"input_tokens":10,"output_tokens":0}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":"","signature":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"signature_delta","signature":"sig_omitted"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":1}}

            event: message_stop
            data: {"type":"message_stop"}

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        observer.Events.ShouldNotContain(e => e is ModelPartDelta);
        var reasoning = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ReasoningPart>();
        reasoning.Content.Text.ShouldBe(string.Empty);
        reasoning.Content.SignatureToken.ShouldBe("sig_omitted");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenRedactedThinkingBlock_EmitsRedactedReasoningPart()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = /*lang=text*/ """
            event: message_start
            data: {"type":"message_start","message":{"id":"message","model":"claude","content":[],"usage":{"input_tokens":10,"output_tokens":0}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"redacted_thinking","data":"opaque_redacted_payload"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":1}}

            event: message_stop
            data: {"type":"message_stop"}

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var reasoning = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ReasoningPart>();
        reasoning.Content.Visibility.ShouldBe(ReasoningVisibility.Redacted);
        reasoning.Content.Text.ShouldBeNull();
        reasoning.Content.SignatureToken.ShouldBe("opaque_redacted_payload");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenContentBlockKindIsUnrecognized_EmitsUnknownContentPart()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = /*lang=text*/ """
            event: message_start
            data: {"type":"message_start","message":{"id":"message","model":"claude","content":[],"usage":{"input_tokens":10,"output_tokens":0}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"server_tool_use","id":"srvtool_1","name":"web_search"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":1}}

            event: message_stop
            data: {"type":"message_stop"}

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var unknown = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<UnknownContentPart>();
        unknown.TypeName.ShouldBe("server_tool_use");
        unknown.Payload.GetProperty("name").GetString().ShouldBe("web_search");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenTextBlockStartsWithNonEmptyText_EmitsInitialDeltaImmediately()
    {
        // Anthropic's documented contract opens text/thinking blocks empty, but a compatible or future dialect
        // could populate the opening block directly; the parser must not silently drop that content.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = /*lang=text*/ """
            event: message_start
            data: {"type":"message_start","message":{"id":"message","model":"claude","content":[],"usage":{"input_tokens":10,"output_tokens":0}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":"Immediate"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":1}}

            event: message_stop
            data: {"type":"message_stop"}

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Immediate");
        var delta = observer.Events.OfType<ModelPartDelta>().ShouldHaveSingleItem().Delta.ShouldBeOfType<TextContentDelta>();
        delta.Text.ShouldBe("Immediate");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenThinkingBlockStartsWithNonEmptyThinking_EmitsInitialDeltaImmediately()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = /*lang=text*/ """
            event: message_start
            data: {"type":"message_start","message":{"id":"message","model":"claude","content":[],"usage":{"input_tokens":10,"output_tokens":0}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":"Already thinking","signature":"sig_start"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":1}}

            event: message_stop
            data: {"type":"message_stop"}

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var reasoning = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ReasoningPart>();
        reasoning.Content.Text.ShouldBe("Already thinking");
        reasoning.Content.SignatureToken.ShouldBe("sig_start");
        var delta = observer.Events.OfType<ModelPartDelta>().ShouldHaveSingleItem().Delta.ShouldBeOfType<ReasoningContentDelta>();
        delta.Text.ShouldBe("Already thinking");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenDeltaTypeIsUnknown_EmitsProviderContentDeltaAndKeepsBlockContent(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        await using var stream = new ChunkedStream(TestResources.ReadAllBytes("responses/streaming_unknown_delta.sse"), chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");
        var deltas = observer.Events.OfType<ModelPartDelta>().Select(e => e.Delta).ToArray();
        deltas.Length.ShouldBe(3);
        deltas[0].ShouldBeOfType<TextContentDelta>().Text.ShouldBe("Hello");
        var providerDelta = deltas[1].ShouldBeOfType<ProviderContentDelta>();
        providerDelta.ProviderId.ShouldBe(new ProviderId("anthropic"));
        var raw = JsonDocument.Parse(providerDelta.Extensions.Values["raw"].CanonicalJson.AsMemory()).RootElement;
        raw.GetProperty("type").GetString().ShouldBe("citations_delta");
        raw.GetProperty("citation").GetProperty("cited_text").GetString().ShouldBe("Hello");
        deltas[2].ShouldBeOfType<TextContentDelta>().Text.ShouldBe("!");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenDeltaArrivesForUnopenedBlock_FailsWithProtocolViolationRetainingPartialParts(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        await using var stream = new ChunkedStream(TestResources.ReadAllBytes("responses/streaming_delta_before_start.sse"), chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldContain("content block 1");
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Partial");
        failed.Usage.ShouldNotBeNull().ReportState.ShouldBe(ModelUsageReportState.Interim);
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        observer.Events.OfType<ModelPartDelta>().Select(e => e.Delta).OfType<TextContentDelta>().Select(d => d.Text).ShouldNotContain("orphaned");
        observer.Events[^1].ShouldBeOfType<ModelResponseFailed>().PartialParts.ShouldBe(failed.PartialParts);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenStopArrivesForUnopenedBlock_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = /*lang=text*/ """
            event: message_start
            data: {"type":"message_start","message":{"id":"message","model":"claude","content":[],"usage":{"input_tokens":10,"output_tokens":0}}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":1}}

            event: message_stop
            data: {"type":"message_stop"}

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldContain("content_block_stop");
        failed.PartialParts.ShouldBeEmpty();
        observer.Events.ShouldNotContain(e => e is ModelPartCompleted);
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenMessageDeltaCarriesStopSequence_PreservesItInResponseExtensions()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = /*lang=text*/ """
            event: message_start
            data: {"type":"message_start","message":{"id":"message","model":"claude","content":[],"usage":{"input_tokens":10,"output_tokens":0}}}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"stop_sequence","stop_sequence":"END"},"usage":{"output_tokens":1}}

            event: message_stop
            data: {"type":"message_stop"}

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.Completed);
        JsonDocument.Parse(completed.Response.Extensions.Values["stop_sequence"].CanonicalJson.AsMemory()).RootElement.GetString().ShouldBe("END");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenStopSequenceIsNull_LeavesResponseExtensionsEmpty()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        await using var stream = new MemoryStream(TestResources.ReadAllBytes("responses/streaming_text.sse"));
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<ModelAttemptCompleted>().Response.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenResponseCarriesStopSequence_PreservesItInResponseExtensions()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/buffered_text.json").Replace("\"stop_sequence\": null", "\"stop_sequence\": \"END\"", StringComparison.Ordinal);
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        JsonDocument.Parse(completed.Response.Extensions.Values["stop_sequence"].CanonicalJson.AsMemory()).RootElement.GetString().ShouldBe("END");
    }
}
