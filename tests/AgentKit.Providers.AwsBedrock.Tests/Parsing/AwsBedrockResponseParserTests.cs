// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests.Parsing;

using System.Text;

using AgentKit.Providers.AwsBedrock.Tests.Fakes;
using AgentKit.TestSupport;

/// <summary>Verifies AwsBedrockResponseParser behavior and contracts.</summary>
public sealed class AwsBedrockResponseParserTests
{
    private static ProviderResponseParseContext CreateContext(ModelRequestId requestId) => new(requestId, AwsBedrockProviderDefaults.ProviderId, AwsBedrockProviderDefaults.ApiFamily, new ModelId("anthropic.claude-3-sonnet-20240229-v1:0"), deploymentId: null, providerRequestId: null);
    [Fact]
    public async Task ParseBufferedAsync_WhenPlainTextResponse_EmitsTextPartAndCompletes()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_text.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.Completed);
        completed.Response.Parts.Length.ShouldBe(1);
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello! How can I help you today?");
        completed.Response.Usage.ReportState.ShouldBe(ModelUsageReportState.Final);
        completed.Response.Usage.InputTokens.ShouldBe(20);
        completed.Response.Usage.OutputTokens.ShouldBe(9);
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
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_tool_use.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.ToolUse);
        completed.Response.Parts.Length.ShouldBe(1);
        var toolCall = completed.Response.Parts[0].ShouldBeOfType<ToolCallPart>();
        toolCall.Tool.ProviderAlias.Value.ShouldBe("get_weather");
        toolCall.Tool.Id.ShouldBeNull();
        toolCall.Tool.IsResolved.ShouldBeFalse();
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("tooluse_01xyz"));
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
        var deltaEvent = observer.Events.OfType<ModelPartDelta>().ShouldHaveSingleItem();
        var argumentsDelta = deltaEvent.Delta.ShouldBeOfType<ToolArgumentsContentDelta>();
        argumentsDelta.ToolCallId.ShouldBe(toolCall.CallId);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenUsageTokenCountIsNegative_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/buffered_text.json").Replace("\"inputTokens\": 20", "\"inputTokens\": -1", StringComparison.Ordinal);
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
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        await using var body = new MemoryStream("not json"u8.ToArray());
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<JsonException>();
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenContentBlockIsNeitherTextNorToolUse_WrapsAsUnknownContentPart()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        await using var body = new MemoryStream(
            /*lang=json,strict*/ """{"output":{"message":{"role":"assistant","content":[{"image":{"format":"png","source":{"bytes":"AAAA"}}}]}},"stopReason":"end_turn"}"""u8.ToArray());

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var unknown = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<UnknownContentPart>();
        unknown.TypeName.ShouldBe("unknown");
    }

    /// <summary>Verifies every documented Bedrock stop reason maps to its normalized stop reason, including unmapped and absent values.</summary>
    [Theory]
    [InlineData("stop_sequence", NormalizedStopReason.Completed)]
    [InlineData("max_tokens", NormalizedStopReason.Length)]
    [InlineData("model_context_window_exceeded", NormalizedStopReason.Length)]
    [InlineData("guardrail_intervened", NormalizedStopReason.Error)]
    [InlineData("content_filtered", NormalizedStopReason.Error)]
    [InlineData("malformed_model_output", NormalizedStopReason.Error)]
    [InlineData("malformed_tool_use", NormalizedStopReason.Error)]
    [InlineData("some_future_reason", NormalizedStopReason.Error)]
    public async Task ParseBufferedAsync_WhenStopReasonVaries_MapsToExpectedNormalizedStopReason(string stopReason, NormalizedStopReason expected)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/buffered_text.json")
            .Replace("\"stopReason\": \"end_turn\"", $"\"stopReason\": \"{stopReason}\"", StringComparison.Ordinal);
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(expected);
    }

    /// <summary>Verifies a response with no stopReason at all is normalized as still pending rather than completed.</summary>
    [Fact]
    public async Task ParseBufferedAsync_WhenStopReasonIsAbsent_MapsToPending()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllText("responses/buffered_text.json")
            .Replace("\"stopReason\": \"end_turn\",", string.Empty, StringComparison.Ordinal);
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.Pending);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenNoContentBlocks_CompletesWithNoParts()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        await using var body = new MemoryStream(/*lang=json,strict*/
        """{"output":{"message":{"role":"assistant","content":[]}},"stopReason":"end_turn"}"""u8.ToArray());
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.Length.ShouldBe(0);
        completed.Response.Usage.ShouldBeSameAs(ModelUsage.NotReported);
    }

    public static TheoryData<int> ChunkSizes => [1, 2, 3, 7, 64, 4096];

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenPlainTextStream_EmitsTextDeltasRegardlessOfFragmentation(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_text.bin");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.Completed);
        completed.Response.Parts.Length.ShouldBe(1);
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello world");
        completed.Response.Usage.ReportState.ShouldBe(ModelUsageReportState.Final);
        completed.Response.Usage.InputTokens.ShouldBe(10);
        completed.Response.Usage.OutputTokens.ShouldBe(5);
        var textDeltas = observer.Events.OfType<ModelPartDelta>().Select(e => e.Delta).OfType<TextContentDelta>().Select(d => d.Text).ToArray();
        textDeltas.ShouldBe(["Hello", " world"]);
        observer.Events.Select(e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(i => (long) i));
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenTextBlockHasNoStartEvent_EmitsTextPart()
    {
        // Converse never sends contentBlockStart for a text block: the block begins with its first delta.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = AwsEventStreamTestEncoder.Concat(
            AwsEventStreamTestEncoder.EncodeEvent("messageStart", /*lang=json,strict*/ """{"role":"assistant"}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":0,"delta":{"text":"Hello"}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":0,"delta":{"text":" world"}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockStop", /*lang=json,strict*/ """{"contentBlockIndex":0}"""),
            AwsEventStreamTestEncoder.EncodeEvent("messageStop", /*lang=json,strict*/ """{"stopReason":"end_turn"}"""),
            AwsEventStreamTestEncoder.EncodeEvent("metadata", /*lang=json,strict*/ """{"usage":{"inputTokens":10,"outputTokens":5,"totalTokens":15}}"""));
        await using var stream = new ChunkedStream(payload, 5);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.Completed);
        completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello world");
        var started = observer.Events.OfType<ModelPartStarted>().ShouldHaveSingleItem();
        started.PartIndex.ShouldBe(0);
        var firstDelta = observer.Events.OfType<ModelPartDelta>().First();
        started.Sequence.ShouldBeLessThan(firstDelta.Sequence);
        observer.Events.OfType<ModelPartDelta>().Select(e => e.Delta).OfType<TextContentDelta>().Select(d => d.Text).ShouldBe(["Hello", " world"]);
        observer.Events.OfType<ModelPartCompleted>().ShouldHaveSingleItem().PartIndex.ShouldBe(0);
        observer.Events.Select(e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(i => (long) i));
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenTextBlockHasExplicitStartEvent_EmitsTextPartWithoutDuplicateStart(int chunkSize)
    {
        // A contentBlockStart with an empty start payload is not what Converse emits for text, but a stream
        // that does carry one must still parse to the same single part and single ModelPartStarted.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_text_with_synthetic_start.bin");
        await using var stream = new ChunkedStream(payload, chunkSize);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello world");
        observer.Events.OfType<ModelPartStarted>().ShouldHaveSingleItem().PartIndex.ShouldBe(0);
        _ = observer.Events.OfType<ModelPartCompleted>().ShouldHaveSingleItem();
        observer.Events.Select(e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(i => (long) i));
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenToolUseDeltaArrivesWithoutStart_FailsWithProtocolViolation()
    {
        // The tool name and provider call ID travel only on contentBlockStart; without it the arguments
        // cannot be attributed to a tool, and a fabricated identity is never produced.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = AwsEventStreamTestEncoder.Concat(
            AwsEventStreamTestEncoder.EncodeEvent("messageStart", /*lang=json,strict*/ """{"role":"assistant"}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":0,"delta":{"text":"Let me check."}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockStop", /*lang=json,strict*/ """{"contentBlockIndex":0}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":1,"delta":{"toolUse":{"input":"{}"}}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockStop", /*lang=json,strict*/ """{"contentBlockIndex":1}"""),
            AwsEventStreamTestEncoder.EncodeEvent("messageStop", /*lang=json,strict*/ """{"stopReason":"tool_use"}"""));
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Let me check.");
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenUntranslatedBlockIsStoppedWithoutAccumulator_IgnoresItAndCompletes()
    {
        // A reasoningContent block opens no accumulator; its deltas and stop must not fail the stream.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = AwsEventStreamTestEncoder.Concat(
            AwsEventStreamTestEncoder.EncodeEvent("messageStart", /*lang=json,strict*/ """{"role":"assistant"}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":0,"delta":{"reasoningContent":{"text":"thinking"}}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockStop", /*lang=json,strict*/ """{"contentBlockIndex":0}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":1,"delta":{"text":"Answer"}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockStop", /*lang=json,strict*/ """{"contentBlockIndex":1}"""),
            AwsEventStreamTestEncoder.EncodeEvent("messageStop", /*lang=json,strict*/ """{"stopReason":"end_turn"}"""));
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Answer");
        observer.Events.OfType<ModelPartStarted>().ShouldHaveSingleItem().PartIndex.ShouldBe(1);
        observer.Events.OfType<ModelPartCompleted>().ShouldHaveSingleItem().PartIndex.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenToolUseStream_AccumulatesFragmentedArgumentsRegardlessOfFragmentation(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_tool_use.bin");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.ToolUse);
        completed.Response.Parts.Length.ShouldBe(1);
        var toolCall = completed.Response.Parts[0].ShouldBeOfType<ToolCallPart>();
        toolCall.Tool.ProviderAlias.Value.ShouldBe("get_weather");
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("tooluse_01xyz"));
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
        var argumentFragments = observer.Events.OfType<ModelPartDelta>().Select(e => e.Delta).OfType<ToolArgumentsContentDelta>().Select(d => d.JsonFragment).ToArray();
        string.Concat(argumentFragments).ShouldBe( /*lang=json,strict*/"""{"location":"Paris"}""");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenTextThenToolUseStream_EmitsPartsInOrderWithDistinctIndices(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_text_and_tool_use.bin");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.Length.ShouldBe(2);
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Let me check.");
        var toolCall = completed.Response.Parts[1].ShouldBeOfType<ToolCallPart>();
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("tooluse_02abc"));
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("NYC");
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenStreamTruncatedBeforeMessageStop_FailsWithProtocolViolation(int chunkSize)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_truncated.bin");
        await using var stream = new ChunkedStream(payload, chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        // The open text block is materialized as far as it was received, never dropped.
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello");
        observer.Events[^1].ShouldBeOfType<ModelResponseFailed>().PartialParts.ShouldBe(failed.PartialParts);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenExceptionFrameArrivesMidStream_FailsWithMappedProviderFailureKind()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_exception.bin");
        await using var stream = new ChunkedStream(payload, 64);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
        failed.Failure.SafeMessage.ShouldBe("Too many requests");
        failed.Failure.ProviderCode.ShouldBe("throttlingException");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenToolArgumentsAreMalformed_ReturnsProtocolFailure()
    {
        // The completed text block is retained; the tool block whose input never became valid JSON is not
        // fabricated as {} and is omitted from the truthful partial parts.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = AwsEventStreamTestEncoder.Concat(
            AwsEventStreamTestEncoder.EncodeEvent("messageStart", /*lang=json,strict*/ """{"role":"assistant"}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":0,"delta":{"text":"Checking."}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockStop", /*lang=json,strict*/ """{"contentBlockIndex":0}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockStart", /*lang=json,strict*/ """{"contentBlockIndex":1,"start":{"toolUse":{"toolUseId":"tooluse_bad","name":"get_weather"}}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":1,"delta":{"toolUse":{"input":"{\"location\":"}}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":1,"delta":{"toolUse":{"input":"\"Paris\",}"}}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockStop", /*lang=json,strict*/ """{"contentBlockIndex":1}"""),
            AwsEventStreamTestEncoder.EncodeEvent("messageStop", /*lang=json,strict*/ """{"stopReason":"tool_use"}"""));
        await using var stream = new ChunkedStream(payload, 7);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned malformed tool-call arguments.");
        _ = failed.Failure.DiagnosticCause.ShouldBeAssignableTo<JsonException>();
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Checking.");
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        observer.Events.OfType<ModelPartCompleted>().ShouldHaveSingleItem().PartIndex.ShouldBe(0);
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
        terminal.PartialParts.ShouldBe(failed.PartialParts);
        observer.Events.Select(e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(i => (long) i));
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenEventPayloadIsMalformedJson_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = AwsEventStreamTestEncoder.Concat(
            AwsEventStreamTestEncoder.EncodeEvent("messageStart", /*lang=json,strict*/ """{"role":"assistant"}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", "{ not valid json"));
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned a malformed streaming event.");
        _ = failed.Failure.DiagnosticCause.ShouldBeAssignableTo<JsonException>();
    }

    /// <summary>Verifies a messageStop received while a content block never got its contentBlockStop fails closed rather than fabricating a completed block.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenMessageStopArrivesWithAnOpenContentBlock_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = AwsEventStreamTestEncoder.Concat(
            AwsEventStreamTestEncoder.EncodeEvent("messageStart", /*lang=json,strict*/ """{"role":"assistant"}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":0,"delta":{"text":"Hello"}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("messageStop", /*lang=json,strict*/ """{"stopReason":"end_turn"}"""));
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider's streaming response ended before content block 0 was closed.");
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello");
    }

    /// <summary>Verifies negative final usage evidence at messageStop fails closed rather than reporting fabricated usage.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenFinalUsageTokenCountIsNegative_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = AwsEventStreamTestEncoder.Concat(
            AwsEventStreamTestEncoder.EncodeEvent("messageStart", /*lang=json,strict*/ """{"role":"assistant"}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":0,"delta":{"text":"Hello world"}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockStop", /*lang=json,strict*/ """{"contentBlockIndex":0}"""),
            AwsEventStreamTestEncoder.EncodeEvent("messageStop", /*lang=json,strict*/ """{"stopReason":"end_turn"}"""),
            AwsEventStreamTestEncoder.EncodeEvent("metadata", /*lang=json,strict*/ """{"usage":{"inputTokens":-1,"outputTokens":5,"totalTokens":4}}"""));
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello world");
    }

    /// <summary>Verifies negative usage retained from an earlier metadata event is dropped (not reported) rather than propagated as a secondary failure when the stream later fails for another reason.</summary>
    [Fact]
    public async Task ParseStreamingAsync_WhenStreamFailsAfterEarlierNegativeUsage_RetainsNoUsage()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = AwsEventStreamTestEncoder.Concat(
            AwsEventStreamTestEncoder.EncodeEvent("messageStart", /*lang=json,strict*/ """{"role":"assistant"}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":0,"delta":{"text":"Hello"}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("metadata", /*lang=json,strict*/ """{"usage":{"inputTokens":-1,"outputTokens":1,"totalTokens":0}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", "{ not valid json"));
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Usage.ShouldBeNull();
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenFrameDeclaresOversizeLength_FailsWithProtocolViolationWithoutAllocating()
    {
        // A hostile prelude after a valid text block: the decoder's bound is surfaced as a typed failure with
        // the already-received text retained, never as an out-of-memory condition or an escaping exception.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = AwsEventStreamTestEncoder.Concat(
            AwsEventStreamTestEncoder.EncodeEvent("messageStart", /*lang=json,strict*/ """{"role":"assistant"}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":0,"delta":{"text":"Hello"}}"""),
            AwsEventStreamTestEncoder.EncodeRawFrame([], "{}"u8, totalLength: 0xFFFF_FFF0));
        await using var stream = new MemoryStream(payload);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        (GC.GetAllocatedBytesForCurrentThread() - allocatedBefore).ShouldBeLessThan(1024 * 1024);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned a malformed event-stream frame.");
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenHeaderValueOverrunsHeadersBlock_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        byte[] malformedHeaders = [3, (byte) ':', (byte) 'a', (byte) 'b', 7, 0x00, 0x10, (byte) 'x'];
        var payload = AwsEventStreamTestEncoder.Concat(
            AwsEventStreamTestEncoder.EncodeEvent("messageStart", /*lang=json,strict*/ """{"role":"assistant"}"""),
            AwsEventStreamTestEncoder.EncodeRawFrame(malformedHeaders, "{}"u8));
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned a malformed event-stream frame.");
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenToolUseStartOmitsToolUseId_LeavesProviderCallIdNull()
    {
        // contentBlockStart's "start.toolUse" DTO declares toolUseId as nullable; when the provider omits it,
        // the accumulated part must not fabricate a provider call identity.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = AwsEventStreamTestEncoder.Concat(
            AwsEventStreamTestEncoder.EncodeEvent("messageStart", /*lang=json,strict*/ """{"role":"assistant"}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockStart", /*lang=json,strict*/ """{"contentBlockIndex":0,"start":{"toolUse":{"name":"get_weather"}}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":0,"delta":{"toolUse":{"input":"{}"}}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockStop", /*lang=json,strict*/ """{"contentBlockIndex":0}"""),
            AwsEventStreamTestEncoder.EncodeEvent("messageStop", /*lang=json,strict*/ """{"stopReason":"tool_use"}"""));
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var toolCall = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolCallPart>();
        toolCall.Tool.ProviderAlias.Value.ShouldBe("get_weather");
        toolCall.ProviderCallId.ShouldBeNull();
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenToolUseStartOmitsName_FallsBackToUnknownToolAlias()
    {
        // contentBlockStart's "start.toolUse" DTO declares name as nullable; when the provider omits it, the
        // accumulated part falls back to the same "unknown" placeholder the buffered parser uses rather than
        // failing the whole block.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = AwsEventStreamTestEncoder.Concat(
            AwsEventStreamTestEncoder.EncodeEvent("messageStart", /*lang=json,strict*/ """{"role":"assistant"}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockStart", /*lang=json,strict*/ """{"contentBlockIndex":0,"start":{"toolUse":{"toolUseId":"tooluse_noname"}}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":0,"delta":{"toolUse":{"input":"{}"}}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockStop", /*lang=json,strict*/ """{"contentBlockIndex":0}"""),
            AwsEventStreamTestEncoder.EncodeEvent("messageStop", /*lang=json,strict*/ """{"stopReason":"tool_use"}"""));
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var toolCall = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolCallPart>();
        toolCall.Tool.ProviderAlias.Value.ShouldBe("unknown");
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("tooluse_noname"));
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenStreamFailsWithAnOpenToolUseBlockOfUnknownShape_PartialPartsOmitTheOpenToolUseBlock()
    {
        // TryBuildPartialPart's own JsonException catch (an incomplete tool-use accumulator) is exercised by
        // omitting messageStop so BuildPartialParts must materialize the still-open tool-use block itself.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = AwsEventStreamTestEncoder.Concat(
            AwsEventStreamTestEncoder.EncodeEvent("messageStart", /*lang=json,strict*/ """{"role":"assistant"}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockStart", /*lang=json,strict*/ """{"contentBlockIndex":0,"start":{"toolUse":{"toolUseId":"tooluse_open","name":"get_weather"}}}"""),
            AwsEventStreamTestEncoder.EncodeEvent("contentBlockDelta", /*lang=json,strict*/ """{"contentBlockIndex":0,"delta":{"toolUse":{"input":"{\"location\":"}}}"""));
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.ShouldBeEmpty();
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenFrameIsCorrupted_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new AwsBedrockResponseParser(new SequentialToolCallIdGenerator());
        var payload = TestResources.ReadAllBytes("responses/streaming_text.bin");
        payload[20] ^= 0xFF;
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }
}
