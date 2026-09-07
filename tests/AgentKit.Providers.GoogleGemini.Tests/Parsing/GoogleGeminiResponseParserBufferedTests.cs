// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests.Parsing;

using AgentKit.Providers.GoogleGemini.Tests.Fakes;

/// <summary>
/// Verifies <see cref="GoogleGeminiResponseParser.ParseBufferedAsync"/>
/// against fixture non-streaming Gemini <c>generateContent</c> response
/// bodies.
/// </summary>
public sealed class GoogleGeminiResponseParserBufferedTests
{
    private static GoogleGeminiResponseParseContext CreateContext(ModelRequestId requestId) =>
        new(
            requestId,
            GoogleGeminiProviderDefaults.ProviderId,
            GoogleGeminiProviderDefaults.ApiFamily,
            new ModelId("gemini-2.5-flash"),
            deploymentId: null,
            providerRequestId: null);

    [Fact]
    public async Task ParseBufferedAsync_WhenPlainTextResponse_EmitsTextPartAndCompletes()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new GoogleGeminiResponseParser(new SequentialToolCallIdGenerator());

        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_text.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.StopReason.ShouldBe(NormalizedStopReason.Completed);
        completed.Response.Parts.Length.ShouldBe(1);
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello! How can I help you today?");

        completed.Response.Usage.InputTokens.ShouldBe(20);
        completed.Response.Usage.OutputTokens.ShouldBe(9);
        completed.Response.Usage.CachedInputTokens.ShouldBeNull();

        completed.Response.Identity.ResolvedModelId.ShouldBe(new ModelId("gemini-2.5-flash-001"));
        completed.Response.Identity.ResponseId.ShouldBe(new ProviderResponseId("resp_01abc"));

        _ = observer.Events[0].ShouldBeOfType<ModelResponseStarted>();
        _ = observer.Events.OfType<ModelPartStarted>().ShouldHaveSingleItem();
        _ = observer.Events.OfType<ModelPartCompleted>().ShouldHaveSingleItem();
        _ = observer.Events.OfType<ModelUsageUpdated>().ShouldHaveSingleItem();
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseCompleted>();

        observer.Events.Select(e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(i => (long) i));
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenToolUseResponse_EmitsToolCallPartAndOverridesStopReasonToToolUse()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new GoogleGeminiResponseParser(new SequentialToolCallIdGenerator());

        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_tool_use.json"));
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
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenThinkingAndTextResponse_EmitsReasoningThenTextParts()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new GoogleGeminiResponseParser(new SequentialToolCallIdGenerator());

        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_thinking.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts.Length.ShouldBe(2);

        var reasoning = completed.Response.Parts[0].ShouldBeOfType<ReasoningPart>();
        reasoning.Content.Visibility.ShouldBe(ReasoningVisibility.Visible);
        reasoning.Content.Text.ShouldBe("Let me consider this carefully...");
        reasoning.Content.SignatureToken.ShouldBe("sig_abc123");

        completed.Response.Parts[1].ShouldBeOfType<TextPart>().Text.ShouldBe("The answer is 42.");
        completed.Response.Usage.ReasoningTokens.ShouldBe(18);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenUnknownPartKind_WrapsAsUnknownContentPart()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new GoogleGeminiResponseParser(new SequentialToolCallIdGenerator());

        await using var body = File.OpenRead(TestResources.GetPath("responses/buffered_unknown_part.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        var unknown = completed.Response.Parts[0].ShouldBeOfType<UnknownContentPart>();
        unknown.TypeName.ShouldBe("unknownPart");
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenPromptWasBlocked_FailsWithInvalidRequestAndBlockReason()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new GoogleGeminiResponseParser(new SequentialToolCallIdGenerator());

        await using var body = File.OpenRead(TestResources.GetPath("responses/blocked_prompt.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failed.Failure.SafeMessage.ShouldBe("The provider blocked the prompt: SAFETY.");
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenNoCandidatesAndNoBlockReason_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new GoogleGeminiResponseParser(new SequentialToolCallIdGenerator());

        await using var body = File.OpenRead(TestResources.GetPath("responses/no_candidates.json"));
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Fact]
    public async Task ParseBufferedAsync_WhenBodyIsNotJson_FailsWithProtocolViolation()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var observer = new RecordingModelResponseObserver();
        var parser = new GoogleGeminiResponseParser(new SequentialToolCallIdGenerator());

        await using var body = new MemoryStream("not json"u8.ToArray());
        var result = await parser.ParseBufferedAsync(body, CreateContext(requestId), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<JsonException>();
    }
}
