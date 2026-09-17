// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Tests.Parsing;

using System.Text;

using AgentKit.Providers.Cohere.Tests.Fakes;
using AgentKit.TestSupport;

/// <summary>Verifies CohereResponseParser behavior and contracts.</summary>
public sealed class CohereResponseParserTests
{
    private static ProviderResponseParseContext CreateContext(ModelRequestId requestId) => new(requestId, CohereProviderDefaults.ProviderId, CohereProviderDefaults.ApiFamily, new ModelId("command-a-plus-05-2026"), deploymentId: null, providerRequestId: null);
    [Fact]
    public async Task ParseBufferedAsync_WhenPlainTextResponse_EmitsTextPartAndCompletes()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
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
        completed.Response.Identity.ResponseId.ShouldBe(new ProviderResponseId("c14c80c3-18eb-4519-9460-6c92edd8cfb4"));
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
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_tool_use.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.ToolUse);
        completed.Response.Parts.Length.ShouldBe(1);
        var toolCall = completed.Response.Parts[0].ShouldBeOfType<ToolCallPart>();
        toolCall.Tool.ProviderAlias.Value.ShouldBe("get_weather");
        toolCall.Tool.Id.ShouldBeNull();
        toolCall.Tool.IsResolved.ShouldBeFalse();
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("get_weather_nsz5zm3w56q3"));
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
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_thinking.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.Length.ShouldBe(2);
        var reasoning = completed.Response.Parts[0].ShouldBeOfType<ReasoningPart>();
        reasoning.Content.Visibility.ShouldBe(ReasoningVisibility.Visible);
        reasoning.Content.Text.ShouldBe("Let me consider this carefully...");
        completed.Response.Parts[1].ShouldBeOfType<TextPart>().Text.ShouldBe("The answer is 42.");
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenUnknownContentBlock_WrapsAsUnknownContentPart()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_unknown_block.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var unknown = completed.Response.Parts[0].ShouldBeOfType<UnknownContentPart>();
        unknown.TypeName.ShouldBe("image");
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenNoMessage_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/no_message.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenUsageTokenCountIsNegativeFraction_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/buffered_text.json").Replace("\"input_tokens\": 20", "\"input_tokens\": -0.5", StringComparison.Ordinal);
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        observer.Events.ShouldNotContain(@event => @event is ModelResponseCompleted);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenToolCallArgumentsAreMalformedJson_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/buffered_tool_use.json")
            .Replace("\"{\\\"location\\\": \\\"Paris\\\"}\"", "\"{bad json\"", StringComparison.Ordinal);
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned malformed tool-call arguments.");
        _ = failed.Failure.DiagnosticCause.ShouldBeAssignableTo<JsonException>();
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenBodyIsNotJson_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
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
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
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
        completed.Response.Identity.ResponseId.ShouldBe(new ProviderResponseId("c14c80c3-stream-01"));
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
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
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
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_tool_use.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.ToolUse);
        completed.Response.Parts.Length.ShouldBe(1);
        var toolCall = completed.Response.Parts[0].ShouldBeOfType<ToolCallPart>();
        toolCall.Tool.ProviderAlias.Value.ShouldBe("get_weather");
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("get_weather_nsz5zm3w56q3"));
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
        var argumentFragments = observer.Events.OfType<ModelPartDelta>().Select(e => e.Delta).OfType<ToolArgumentsContentDelta>().Select(d => d.JsonFragment).ToArray();
        string.Concat(argumentFragments).ShouldBe( /*lang=json,strict*/"""{"location":"Paris"}""");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenToolCallEventsNeverCarryFunctionName_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"type": "message-start", "id": "c14c80c3-noname", "delta": {"message": {"role": "assistant"}}}

            data: {"type": "tool-call-start", "index": 0, "delta": {"message": {"tool_calls": {"id": "call_noname", "type": "function", "function": {"arguments": ""}}}}}

            data: {"type": "tool-call-delta", "index": 0, "delta": {"message": {"tool_calls": {"function": {"arguments": "{\"location\":\"Paris\"}"}}}}}

            data: {"type": "tool-call-end", "index": 0}

            data: {"type": "message-end", "delta": {"finish_reason": "TOOL_CALL", "usage": {"tokens": {"input_tokens": 8, "output_tokens": 5}}}}


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.OfType<ToolCallPart>().ShouldBeEmpty();
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenNamelessToolCallIsStillOpenAtMessageEnd_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"type": "message-start", "id": "c14c80c3-noname-open", "delta": {"message": {"role": "assistant"}}}

            data: {"type": "tool-call-start", "index": 0, "delta": {"message": {"tool_calls": {"id": "call_noname", "type": "function", "function": {"arguments": "{}"}}}}}

            data: {"type": "message-end", "delta": {"finish_reason": "TOOL_CALL", "usage": {"tokens": {"input_tokens": 8, "output_tokens": 5}}}}


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.OfType<ToolCallPart>().ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenParallelToolCallsStream_CorrelatesFragmentsByWireIndex(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
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
    public async Task ParseStreamingAsync_WhenThinkingThenTextStream_EmitsReasoningThenTextPartsInOrder(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_thinking.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.Length.ShouldBe(2);
        var reasoning = completed.Response.Parts[0].ShouldBeOfType<ReasoningPart>();
        reasoning.Content.Text.ShouldBe("Let me consider...");
        completed.Response.Parts[1].ShouldBeOfType<TextPart>().Text.ShouldBe("The answer is 42.");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenUnknownContentBlock_WrapsAsUnknownContentPart(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_unknown_block.sse");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var unknown = completed.Response.Parts[0].ShouldBeOfType<UnknownContentPart>();
        unknown.TypeName.ShouldBe("image");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenStreamEndsWithoutMessageEnd_FailsWithProtocolViolation(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
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
    public async Task ParseStreamingAsync_WhenEventIsMalformedJson_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = "data: { not valid json\n\n"u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    /// <summary>Verifies unmodeled wire fields (a content event's tool-use plan text, and message-end's error text) deserialize without disturbing the normal parse outcome.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenEventsCarryToolPlanAndErrorFieldsNotYetRoundTripped_StillCompletesNormally()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"type": "message-start", "id": "c14c80c3-unmodeled-fields", "delta": {"message": {"role": "assistant", "tool_plan": "I will answer directly."}}}

            data: {"type": "content-start", "index": 0, "delta": {"message": {"content": {"type": "text", "text": ""}}}}

            data: {"type": "content-delta", "index": 0, "delta": {"message": {"content": {"text": "Hello"}}}}

            data: {"type": "content-end", "index": 0}

            data: {"type": "message-end", "delta": {"finish_reason": "COMPLETE", "error": null, "usage": {"tokens": {"input_tokens": 1, "output_tokens": 1}}}}


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenMessageEndCarriesAnErrorText_FailsInsteadOfCompleting()
    {
        // CohereStreamEventDeltaDto.Error is present only on message-end and reports why generation
        // failed; dropping it and completing normally would lose the only evidence of what went wrong.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"type": "message-start", "id": "c14c80c3-error", "delta": {"message": {"role": "assistant"}}}

            data: {"type": "content-start", "index": 0, "delta": {"message": {"content": {"type": "text", "text": ""}}}}

            data: {"type": "content-delta", "index": 0, "delta": {"message": {"content": {"text": "Partial"}}}}

            data: {"type": "content-end", "index": 0}

            data: {"type": "message-end", "delta": {"finish_reason": "ERROR", "error": "internal model error", "usage": {"tokens": {"input_tokens": 1, "output_tokens": 1}}}}


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.SafeMessage.ShouldBe("internal model error");
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Partial");
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenFinishReasonIsTimeoutWithoutErrorText_FailsWithTimeoutKind()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"type": "message-start", "id": "c14c80c3-timeout", "delta": {"message": {"role": "assistant"}}}

            data: {"type": "message-end", "delta": {"finish_reason": "TIMEOUT", "usage": {"tokens": {"input_tokens": 1, "output_tokens": 0}}}}


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
    }

    /// <summary>Verifies a content-delta arriving without a preceding content-start still infers its slot kind and materializes it.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenContentDeltaArrivesWithoutContentStart_InfersTextSlotAndMaterializesIt()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"type": "message-start", "id": "c14c80c3-no-content-start", "delta": {"message": {"role": "assistant"}}}

            data: {"type": "content-delta", "index": 0, "delta": {"message": {"content": {"text": "Hi"}}}}

            data: {"type": "content-end", "index": 0}

            data: {"type": "message-end", "delta": {"finish_reason": "COMPLETE", "usage": {"tokens": {"input_tokens": 1, "output_tokens": 1}}}}


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hi");
    }

    /// <summary>Verifies a tool-call-delta arriving without a preceding tool-call-start still accumulates and materializes the call.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenToolCallDeltaArrivesWithoutToolCallStart_AccumulatesAndMaterializesTheCall()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"type": "message-start", "id": "c14c80c3-no-tool-start", "delta": {"message": {"role": "assistant"}}}

            data: {"type": "tool-call-delta", "index": 0, "delta": {"message": {"tool_calls": {"id": "call_x", "type": "function", "function": {"name": "get_weather", "arguments": "{}"}}}}}

            data: {"type": "tool-call-end", "index": 0}

            data: {"type": "message-end", "delta": {"finish_reason": "TOOL_CALL", "usage": {"tokens": {"input_tokens": 1, "output_tokens": 1}}}}


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var toolCall = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolCallPart>();
        toolCall.Tool.ProviderAlias.Value.ShouldBe("get_weather");
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("call_x"));
    }

    /// <summary>Verifies malformed accumulated tool-call arguments discovered at a mid-stream tool-call-end fail closed rather than fabricating {}.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenToolCallEndArgumentsAreMalformedJson_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"type": "message-start", "id": "c14c80c3-bad-args", "delta": {"message": {"role": "assistant"}}}

            data: {"type": "tool-call-start", "index": 0, "delta": {"message": {"tool_calls": {"id": "call_bad", "type": "function", "function": {"name": "get_weather", "arguments": "{bad json"}}}}}

            data: {"type": "tool-call-end", "index": 0}

            data: {"type": "message-end", "delta": {"finish_reason": "TOOL_CALL", "usage": {"tokens": {"input_tokens": 1, "output_tokens": 1}}}}


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned malformed tool-call arguments.");
        failed.PartialParts.OfType<ToolCallPart>().ShouldBeEmpty();
    }

    /// <summary>Verifies malformed accumulated tool-call arguments discovered only at final flush (no tool-call-end ever arrived) fail closed.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenToolCallNeverClosedAndArgumentsAreMalformedAtMessageEnd_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"type": "message-start", "id": "c14c80c3-bad-args-open", "delta": {"message": {"role": "assistant"}}}

            data: {"type": "tool-call-start", "index": 0, "delta": {"message": {"tool_calls": {"id": "call_bad", "type": "function", "function": {"name": "get_weather", "arguments": "{bad json"}}}}}

            data: {"type": "message-end", "delta": {"finish_reason": "TOOL_CALL", "usage": {"tokens": {"input_tokens": 1, "output_tokens": 1}}}}


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.OfType<ToolCallPart>().ShouldBeEmpty();
    }

    /// <summary>Verifies a content block that never received an explicit content-end is still implicitly closed and materialized at message-end.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenContentBlockNeverReceivesExplicitContentEnd_IsImplicitlyClosedAtMessageEnd()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"type": "message-start", "id": "c14c80c3-no-content-end", "delta": {"message": {"role": "assistant"}}}

            data: {"type": "content-start", "index": 0, "delta": {"message": {"content": {"type": "text", "text": ""}}}}

            data: {"type": "content-delta", "index": 0, "delta": {"message": {"content": {"text": "Hello"}}}}

            data: {"type": "message-end", "delta": {"finish_reason": "COMPLETE", "usage": {"tokens": {"input_tokens": 1, "output_tokens": 1}}}}


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello");
    }

    /// <summary>Verifies negative streaming usage evidence discovered at final usage construction fails closed rather than reporting fabricated usage.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenFinalUsageTokenCountIsNegativeFraction_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/streaming_text.sse")
            .Replace("\"input_tokens\": 10", "\"input_tokens\": -0.5", StringComparison.Ordinal);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
    }

    /// <summary>Verifies a content-delta arriving after its slot's content-end is a no-op rather than reopening or corrupting the closed part.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenContentDeltaArrivesAfterContentEnd_IsIgnoredWithoutReopeningTheSlot()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"type": "message-start", "id": "c14c80c3-late-delta", "delta": {"message": {"role": "assistant"}}}

            data: {"type": "content-start", "index": 0, "delta": {"message": {"content": {"type": "text", "text": ""}}}}

            data: {"type": "content-delta", "index": 0, "delta": {"message": {"content": {"text": "Hello"}}}}

            data: {"type": "content-end", "index": 0}

            data: {"type": "content-delta", "index": 0, "delta": {"message": {"content": {"text": "!!!"}}}}

            data: {"type": "message-end", "delta": {"finish_reason": "COMPLETE", "usage": {"tokens": {"input_tokens": 1, "output_tokens": 1}}}}


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello");
    }

    /// <summary>Verifies a tool-call-delta arriving after its slot's tool-call-end is a no-op rather than reopening or corrupting the closed call.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenToolCallDeltaArrivesAfterToolCallEnd_IsIgnoredWithoutReopeningTheSlot()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"type": "message-start", "id": "c14c80c3-late-tool-delta", "delta": {"message": {"role": "assistant"}}}

            data: {"type": "tool-call-start", "index": 0, "delta": {"message": {"tool_calls": {"id": "call_x", "type": "function", "function": {"name": "get_weather", "arguments": "{}"}}}}}

            data: {"type": "tool-call-end", "index": 0}

            data: {"type": "tool-call-delta", "index": 0, "delta": {"message": {"tool_calls": {"function": {"arguments": "IGNORED"}}}}}

            data: {"type": "message-end", "delta": {"finish_reason": "TOOL_CALL", "usage": {"tokens": {"input_tokens": 1, "output_tokens": 1}}}}


            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var toolCall = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolCallPart>();
        toolCall.Arguments.GetRawText().ShouldBe("{}");
    }

    /// <summary>Verifies every documented Cohere finish reason maps to its normalized stop reason, including unmapped and absent values.</summary>
    [Theory]
    [InlineData("STOP_SEQUENCE", NormalizedStopReason.Completed)]
    [InlineData("MAX_TOKENS", NormalizedStopReason.Length)]
    [InlineData("ERROR", NormalizedStopReason.Error)]
    [InlineData("TIMEOUT", NormalizedStopReason.Error)]
    [InlineData("SOME_FUTURE_REASON", NormalizedStopReason.Error)]
    public async Task ParseBufferedAsync_WhenFinishReasonVaries_MapsToExpectedNormalizedStopReason(string finishReason, NormalizedStopReason expected)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/buffered_text.json")
            .Replace("\"finish_reason\": \"COMPLETE\"", $"\"finish_reason\": \"{finishReason}\"", StringComparison.Ordinal);
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
        var parser = new CohereResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/buffered_text.json")
            .Replace("\"finish_reason\": \"COMPLETE\",", string.Empty, StringComparison.Ordinal);
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.Pending);
    }
}
