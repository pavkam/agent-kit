// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests.Parsing;

using AgentKit.Providers.MistralAI.Tests.Fakes;

/// <summary>
/// Verifies <see cref="MistralAIResponseParser.ParseStreamingAsync"/>
/// against fixture server-sent-events Mistral AI Chat Completions response
/// bodies, including at arbitrary byte-fragmentation boundaries.
/// </summary>
public sealed class MistralAIResponseParserStreamingTests
{
    public static TheoryData<int> ChunkSizes => [1, 2, 3, 7, 64, 4096];

    private static MistralAIResponseParseContext CreateContext(ModelRequestId requestId) =>
        new(
            requestId,
            MistralAIProviderDefaults.ProviderId,
            MistralAIProviderDefaults.ApiFamily,
            new ModelId("mistral-large-latest"),
            deploymentId: null,
            providerRequestId: null);

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
        completed.Response.Usage.InputTokens.ShouldBe(10);
        completed.Response.Usage.OutputTokens.ShouldBe(2);

        var textDeltas = observer.Events
            .OfType<ModelPartDelta>()
            .Select(e => e.Delta)
            .OfType<TextContentDelta>()
            .Select(d => d.Text)
            .ToArray();
        textDeltas.ShouldBe(["Hello", "!"]);

        observer.Events.Select(e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(i => (long) i));
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
        toolCall.Tool.Name.ShouldBe("get_weather");
        toolCall.ProviderCallId.ShouldBe(new ProviderToolCallId("call_stream01"));
        toolCall.Arguments.GetProperty("location").GetString().ShouldBe("Paris");

        var argumentFragments = observer.Events
            .OfType<ModelPartDelta>()
            .Select(e => e.Delta)
            .OfType<ToolArgumentsContentDelta>()
            .Select(d => d.JsonFragment)
            .ToArray();
        string.Concat(argumentFragments).ShouldBe(/*lang=json,strict*/ """{"location":"Paris"}""");
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
        first.Tool.Name.ShouldBe("get_weather");
        first.ProviderCallId.ShouldBe(new ProviderToolCallId("call_a"));
        first.Arguments.GetProperty("location").GetString().ShouldBe("Paris");

        var second = completed.Response.Parts[1].ShouldBeOfType<ToolCallPart>();
        second.Tool.Name.ShouldBe("get_time");
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
}
