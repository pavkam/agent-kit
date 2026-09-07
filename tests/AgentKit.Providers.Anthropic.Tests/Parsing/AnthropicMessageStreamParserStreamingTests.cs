// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Tests.Parsing;

using AgentKit.Providers.Anthropic.Tests.Fakes;

/// <summary>
/// Verifies <see cref="AnthropicMessageStreamParser.ParseStreamingAsync"/>
/// against fixture server-sent-events Anthropic Messages response bodies,
/// including at arbitrary byte-fragmentation boundaries.
/// </summary>
public sealed class AnthropicMessageStreamParserStreamingTests
{
    public static TheoryData<int> ChunkSizes => [1, 2, 3, 7, 64, 4096];

    private static AnthropicResponseParseContext CreateContext(ModelRequestId requestId) =>
        new(
            requestId,
            new ProviderId("anthropic"),
            new ApiFamilyId("anthropic-messages"),
            new ModelId("claude-sonnet-4-5"),
            deploymentId: null,
            providerRequestId: null);

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

        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
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
