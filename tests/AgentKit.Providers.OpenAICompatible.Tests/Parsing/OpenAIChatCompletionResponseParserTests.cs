// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Parsing;

using AgentKit.Providers.OpenAICompatible.Tests.Fakes;
using AgentKit.TestSupport;

/// <summary>Verifies OpenAIChatCompletionResponseParser behavior and contracts.</summary>
public sealed class OpenAIChatCompletionResponseParserTests
{
    private static ProviderResponseParseContext CreateContext(ModelRequestId requestId, ProviderRequestId? providerRequestId = null) => new(requestId, new ProviderId("openai"), new ApiFamilyId("openai-chat-completions"), new ModelId("gpt-4o"), deploymentId: null, providerRequestId);
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
        toolCall.Tool.ProviderAlias.Value.ShouldBe("get_weather");
        toolCall.Tool.Id.ShouldBeNull();
        toolCall.Tool.IsResolved.ShouldBeFalse();
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
        first.Tool.ProviderAlias.Value.ShouldBe("get_weather");
        first.ProviderCallId.ShouldBe(new ProviderToolCallId("call_alpha"));
        var second = completed.Response.Parts[1].ShouldBeOfType<ToolCallPart>();
        second.Tool.ProviderAlias.Value.ShouldBe("get_time");
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
    public async Task ParseBufferedAsync_WhenMessageCarriesReasoningContent_EmitsReasoningPartBeforeText()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        const string json = /*lang=json,strict*/ """
            {"id":"chatcmpl-reasoning","model":"gpt-4o","choices":[{"index":0,"message":{"role":"assistant","content":"The answer is 4.","reasoning_content":"2 + 2 = 4"},"finish_reason":"stop"}],"usage":{"prompt_tokens":5,"completion_tokens":4,"total_tokens":9}}
            """;
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.Length.ShouldBe(2);
        var reasoningPart = completed.Response.Parts[0].ShouldBeOfType<ReasoningPart>();
        reasoningPart.Content.Text.ShouldBe("2 + 2 = 4");
        var textPart = completed.Response.Parts[1].ShouldBeOfType<TextPart>();
        textPart.Text.ShouldBe("The answer is 4.");
        var deltaEvents = observer.Events.OfType<ModelPartDelta>().ToArray();
        _ = deltaEvents[0].Delta.ShouldBeOfType<ReasoningContentDelta>();
        _ = deltaEvents[1].Delta.ShouldBeOfType<TextContentDelta>();
        observer.Events.Select(e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(i => (long) i));
    }

    public static TheoryData<string?, NormalizedStopReason> FinishReasonMappings => new()
    {
        { "length", NormalizedStopReason.Length },
        { "content_filter", NormalizedStopReason.Error },
        { null, NormalizedStopReason.Pending },
        { "some_unrecognized_reason", NormalizedStopReason.Error },
    };

    [Theory]
    [MemberData(nameof(FinishReasonMappings))]
    public async Task ParseBufferedAsync_WhenFinishReasonVaries_MapsToExpectedStopReason(string? finishReason, NormalizedStopReason expected)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var finishReasonJson = finishReason is null ? "null" : $"\"{finishReason}\"";
        var json = "{\"id\":\"chatcmpl-fr\",\"model\":\"gpt-4o\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"ok\"},\"finish_reason\":"
            + finishReasonJson
            + "}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1,\"total_tokens\":2}}";
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(expected);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenToolCallArgumentsAreWhitespace_ParsesAsEmptyObject()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        const string json = /*lang=json,strict*/ """
            {"id":"chatcmpl-empty-args","model":"gpt-4o","choices":[{"index":0,"message":{"role":"assistant","tool_calls":[{"id":"call_1","type":"function","function":{"name":"get_weather","arguments":"   "}}]},"finish_reason":"tool_calls"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}
            """;
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var toolCall = completed.Response.Parts[0].ShouldBeOfType<ToolCallPart>();
        toolCall.Arguments.ValueKind.ShouldBe(JsonValueKind.Object);
        toolCall.Arguments.EnumerateObject().Any().ShouldBeFalse();
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenUnindexedToolCallFragmentHasNoId_ContinuesMostRecentlyOpenedSlot()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            """
            data: {"id":"chatcmpl-unindexed","object":"chat.completion.chunk","created":1700000200,"model":"gpt-4o","choices":[{"index":0,"delta":{"role":"assistant","tool_calls":[{"id":"call_only","type":"function","function":{"name":"get_weather","arguments":"{\"lo"}}]},"finish_reason":null}]}

            data: {"id":"chatcmpl-unindexed","object":"chat.completion.chunk","created":1700000200,"model":"gpt-4o","choices":[{"index":0,"delta":{"tool_calls":[{"function":{"arguments":"cation\":\"Paris\"}"}}]},"finish_reason":"tool_calls"}]}

            data: [DONE]

            """);
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var toolCall = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolCallPart>();
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("call_only"));
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
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
    public async Task ParseBufferedAsync_WhenResponseHasMultipleChoices_FailsWithProtocolViolationInsteadOfDroppingCandidates()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        const string json = /*lang=json,strict*/ """
            {"id":"chatcmpl-n2","model":"gpt-4o","choices":[{"index":0,"message":{"role":"assistant","content":"first"},"finish_reason":"stop"},{"index":1,"message":{"role":"assistant","content":"second"},"finish_reason":"stop"}],"usage":{"prompt_tokens":5,"completion_tokens":4,"total_tokens":9}}
            """;
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned more than one choice for a single-candidate request.");
        failed.PartialParts.ShouldBeEmpty();
        observer.Events.OfType<ModelPartStarted>().ShouldBeEmpty();
        observer.Events.OfType<ModelResponseCompleted>().ShouldBeEmpty();
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

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenToolCallDeltasLackIndex_KeysSlotsByIdAndKeepsBothCallsIntact(int chunkSize)
    {
        // Several OpenAI-compatible servers omit `index`; two calls with distinct ids must never merge into one.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        await using var stream = new ChunkedStream(TestResources.ReadAllBytes("responses/streaming_tool_calls_without_index.sse"), chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.ToolUse);
        var toolCalls = completed.Response.Parts.OfType<ToolCallPart>().ToArray();
        toolCalls.Length.ShouldBe(2);
        toolCalls.Select(static part => part.ProviderCallId?.Value).ShouldBe(["call_a", "call_b"]);
        toolCalls[0].Tool.ProviderAlias.Value.ShouldBe("get_weather");
        toolCalls[0].Arguments.GetProperty("location").GetString().ShouldBe("Paris");
        toolCalls[1].Tool.ProviderAlias.Value.ShouldBe("get_time");
        toolCalls[1].Arguments.GetProperty("timezone").GetString().ShouldBe("UTC");
        toolCalls[0].CallId.ShouldNotBe(toolCalls[1].CallId);
        // Each id-keyed slot owns a distinct part index and its own start/delta/complete events.
        observer.Events.OfType<ModelPartStarted>().Select(static e => e.PartIndex).ShouldBe([1, 2]);
        observer.Events.OfType<ModelPartCompleted>().Select(static e => e.PartIndex).ShouldBe([1, 2]);
        observer.Events.OfType<ModelPartDelta>().Select(static e => e.Delta).OfType<ToolArgumentsContentDelta>().Select(static d => d.ToolCallId).Distinct().Count().ShouldBe(2);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseStreamingAsync_WhenUnindexedDeltasResendTheSameId_ContinuesTheSameSlot(int chunkSize)
    {
        // A repeated id on later fragments is a continuation, not a new call; the bound id is never overwritten.
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        await using var stream = new ChunkedStream(TestResources.ReadAllBytes("responses/streaming_tool_call_id_resent_without_index.sse"), chunkSize);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var toolCall = completed.Response.Parts.ShouldHaveSingleItem().ShouldBeOfType<ToolCallPart>();
        toolCall.Tool.ProviderAlias.Value.ShouldBe("get_weather");
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("call_a"));
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
        _ = observer.Events.OfType<ModelPartStarted>().ShouldHaveSingleItem();
        observer.Events.OfType<ModelPartDelta>().Select(static e => e.Delta).OfType<ToolArgumentsContentDelta>().Select(static d => d.ToolCallId).Distinct().ShouldHaveSingleItem().ShouldBe(toolCall.CallId);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenToolCallSlotFinishesWithoutName_FailsWithProtocolViolation()
    {
        // A provider call id is not a tool name; the parser must not fabricate a ToolId from it or from "unknown".
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        await using var stream = new ChunkedStream(TestResources.ReadAllBytes("responses/streaming_tool_call_without_name.sse"), 4096);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider streamed a tool call without a function name.");
        failed.PartialParts.ShouldBeEmpty();
        observer.Events.OfType<ModelPartCompleted>().ShouldBeEmpty();
        observer.Events.OfType<ModelResponseCompleted>().ShouldBeEmpty();
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenFirstToolCallDeltaHasNeitherIndexNorId_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = """
            data: {"id":"chatcmpl-13","object":"chat.completion.chunk","created":1700000130,"model":"local-model","choices":[{"index":0,"delta":{"role":"assistant","content":"Hel","tool_calls":[{"type":"function","function":{"name":"get_weather","arguments":"{}"}}]},"finish_reason":null}]}

            data: [DONE]

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider streamed a tool-call fragment that cannot be attributed to any tool call.");
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hel");
        observer.Events.OfType<ModelPartStarted>().Select(static e => e.PartIndex).ShouldBe([0]);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenIndexedDeltaCarriesConflictingId_FailsWithProtocolViolationWithoutOverwritingBoundId()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = """
            data: {"id":"chatcmpl-14","object":"chat.completion.chunk","created":1700000140,"model":"local-model","choices":[{"index":0,"delta":{"role":"assistant","tool_calls":[{"index":0,"id":"call_a","type":"function","function":{"name":"get_weather","arguments":"{}"}}]},"finish_reason":null}]}

            data: {"id":"chatcmpl-14","object":"chat.completion.chunk","created":1700000140,"model":"local-model","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call_b","type":"function","function":{"arguments":""}}]},"finish_reason":null}]}

            data: [DONE]

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider streamed conflicting tool-call identifiers for one tool-call index.");
        // The slot keeps the id it was first bound to; the conflicting fragment never rebinds it.
        var partial = failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<ToolCallPart>();
        partial.ProviderCallId.ShouldBe(new ProviderToolCallId("call_a"));
        partial.Tool.ProviderAlias.Value.ShouldBe("get_weather");
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
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hel");
    }

    public static TheoryData<string, ProviderFailureKind> StreamingErrorFrameKinds => new()
    {
        { "authentication_error", ProviderFailureKind.Authentication },
        { "permission_error", ProviderFailureKind.Authorization },
        { "invalid_request_error", ProviderFailureKind.InvalidRequest },
        { "server_error", ProviderFailureKind.Unavailable },
        { "overloaded_error", ProviderFailureKind.Unavailable },
        { "some_unrecognized_error", ProviderFailureKind.Unknown },
    };

    [Theory]
    [MemberData(nameof(StreamingErrorFrameKinds))]
    public async Task ParseStreamingAsync_WhenChunkContainsErrorObject_MapsErrorTypeToExpectedKind(string errorType, ProviderFailureKind expectedKind)
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = Encoding.UTF8.GetBytes(
            "data: {\"error\":{\"message\":\"failure\",\"type\":\"" + errorType + "\",\"code\":\"" + errorType + "\"}}\n\ndata: [DONE]\n\n");
        await using var stream = new MemoryStream(payload);

        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(expectedKind);
        failed.Failure.ProviderCode.ShouldBe(errorType);
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
        toolCall.Tool.ProviderAlias.Value.ShouldBe("get_weather");
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
        first.Tool.ProviderAlias.Value.ShouldBe("get_weather");
        first.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
        var second = completed.Response.Parts[1].ShouldBeOfType<ToolCallPart>();
        second.Tool.ProviderAlias.Value.ShouldBe("get_time");
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
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
        // The text was streamed but never completed; the failure must still carry it as truthful partial output.
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Partial");
        terminal.PartialParts.ShouldBe(failed.PartialParts);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenStreamTruncatedAfterUsage_RetainsReportedUsageWithPartialParts()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = """
            data: {"id":"chatcmpl-4","object":"chat.completion.chunk","created":1700000040,"model":"gpt-4o-2024-08-06","choices":[{"index":0,"delta":{"role":"assistant","content":"Partial"},"finish_reason":null}],"usage":{"prompt_tokens":10,"completion_tokens":1,"total_tokens":11}}


            """u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Partial");
        failed.Usage.ShouldNotBeNull().InputTokens.ShouldBe(10);
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
        terminal.Usage.ShouldBe(failed.Usage);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenUsageIsInvalidMidStream_FailsWithPartialPartsRetained()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = """
            data: {"id":"chatcmpl-5","object":"chat.completion.chunk","created":1700000050,"model":"gpt-4o-2024-08-06","choices":[{"index":0,"delta":{"role":"assistant","reasoning_content":"Thinking"},"finish_reason":null}]}

            data: {"id":"chatcmpl-5","object":"chat.completion.chunk","created":1700000050,"model":"gpt-4o-2024-08-06","choices":[{"index":0,"delta":{"content":"Partial"},"finish_reason":null}]}

            data: {"id":"chatcmpl-5","object":"chat.completion.chunk","created":1700000050,"model":"gpt-4o-2024-08-06","choices":[],"usage":{"prompt_tokens":-1,"completion_tokens":1,"total_tokens":0}}

            data: [DONE]

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider returned invalid usage evidence.");
        // Reasoning precedes text, matching the order a completed response would have used.
        failed.PartialParts.Length.ShouldBe(2);
        failed.PartialParts[0].ShouldBeOfType<ReasoningPart>().Content.Text.ShouldBe("Thinking");
        failed.PartialParts[1].ShouldBeOfType<TextPart>().Text.ShouldBe("Partial");
        // Invalid usage is never reported as retained evidence.
        failed.Usage.ShouldBeNull();
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
        terminal.PartialParts.ShouldBe(failed.PartialParts);
        observer.Events.ShouldNotContain(e => e is ModelPartCompleted);
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenStreamTruncatedDuringToolCall_RetainsOnlyToolCallsWithCompleteArguments()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = """
            data: {"id":"chatcmpl-6","object":"chat.completion.chunk","created":1700000060,"model":"gpt-4o-2024-08-06","choices":[{"index":0,"delta":{"role":"assistant","tool_calls":[{"index":0,"id":"call_a","type":"function","function":{"name":"get_weather","arguments":"{\"location\":\"Paris\"}"}}]},"finish_reason":null}]}

            data: {"id":"chatcmpl-6","object":"chat.completion.chunk","created":1700000060,"model":"gpt-4o-2024-08-06","choices":[{"index":0,"delta":{"tool_calls":[{"index":1,"id":"call_b","type":"function","function":{"name":"get_time","arguments":"{\"timezone\":"}}]},"finish_reason":null}]}


            """u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        // The first call's arguments are complete JSON; the second is cut mid-object and cannot be represented truthfully.
        var toolCall = failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<ToolCallPart>();
        toolCall.Tool.ProviderAlias.Value.ShouldBe("get_weather");
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("call_a"));
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("Paris");
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenChunkCarriesSecondChoiceIndex_FailsWithProtocolViolationRetainingFirstCandidateText()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = """
            data: {"id":"chatcmpl-n3","object":"chat.completion.chunk","created":1700000150,"model":"gpt-4o-2024-08-06","choices":[{"index":0,"delta":{"role":"assistant","content":"first"},"finish_reason":null}]}

            data: {"id":"chatcmpl-n3","object":"chat.completion.chunk","created":1700000150,"model":"gpt-4o-2024-08-06","choices":[{"index":1,"delta":{"role":"assistant","content":"second"},"finish_reason":null}]}

            data: {"id":"chatcmpl-n3","object":"chat.completion.chunk","created":1700000150,"model":"gpt-4o-2024-08-06","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

            data: [DONE]

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider streamed a choice other than the single requested candidate.");
        // The second candidate's text is never merged into choice 0's part.
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("first");
        observer.Events.OfType<ModelPartDelta>().Select(static e => e.Delta).OfType<TextContentDelta>().Select(static d => d.Text).ShouldBe(["first"]);
        observer.Events.OfType<ModelResponseCompleted>().ShouldBeEmpty();
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    [Fact]
    public async Task ParseStreamingAsync_WhenChunkCarriesMultipleChoices_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator());
        var payload = """
            data: {"id":"chatcmpl-n4","object":"chat.completion.chunk","created":1700000160,"model":"gpt-4o-2024-08-06","choices":[{"index":0,"delta":{"role":"assistant","content":"a"},"finish_reason":null},{"index":1,"delta":{"role":"assistant","content":"b"},"finish_reason":null}]}

            data: [DONE]

            """u8.ToArray();
        await using var stream = new MemoryStream(payload);
        var result = await parser.ParseStreamingAsync(stream, CreateContext(requestId), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.SafeMessage.ShouldBe("The provider streamed a choice other than the single requested candidate.");
        failed.PartialParts.ShouldBeEmpty();
        observer.Events.OfType<ModelPartStarted>().ShouldBeEmpty();
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
