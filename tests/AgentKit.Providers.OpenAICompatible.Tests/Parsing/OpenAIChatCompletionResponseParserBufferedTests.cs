// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Parsing;

using AgentKit.Providers.OpenAICompatible.Tests.Fakes;

/// <summary>
/// Verifies <see cref="OpenAIChatCompletionResponseParser.ParseBufferedAsync"/>
/// against fixture non-streaming OpenAI-compatible response bodies.
/// </summary>
public sealed class OpenAIChatCompletionResponseParserBufferedTests
{
    private static OpenAIResponseParseContext CreateContext(
        ModelRequestId requestId,
        ProviderRequestId? providerRequestId = null) =>
        new(
            requestId,
            new ProviderId("openai"),
            new ApiFamilyId("openai-chat-completions"),
            new ModelId("gpt-4o"),
            deploymentId: null,
            providerRequestId);

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
        argumentsDelta.JsonFragment.ShouldBe(/*lang=json,strict*/ """{"location":"Paris"}""");
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

        await using var body = File.OpenRead(
            TestResources.GetPath("responses/buffered_malformed_tool_arguments.json"));
        var result = await parser.ParseBufferedAsync(
            body,
            CreateContext(requestId, providerRequestId),
            observer,
            TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.Failure.RequestId.ShouldBe(providerRequestId);
        failed.Failure.DiagnosticCause.ShouldBeNull();
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("partial text");
        failed.Usage.ShouldNotBeNull().InputTokens.ShouldBe(30);

        var argumentDelta = observer.Events
            .OfType<ModelPartDelta>()
            .Select(responseEvent => responseEvent.Delta)
            .OfType<ToolArgumentsContentDelta>()
            .ShouldHaveSingleItem();
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
        terminal.RequestId.ShouldBe(requestId);
        terminal.Failure.ShouldBe(failed.Failure);
        terminal.PartialParts.ShouldBe(failed.PartialParts);
        observer.Events.Select(responseEvent => responseEvent.Sequence).ShouldBe(
            Enumerable.Range(0, observer.Events.Count).Select(index => (long) index));
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
        completed.Response.Usage.ShouldBeSameAs(ModelUsage.Empty);
        observer.Events.OfType<ModelUsageUpdated>().ShouldBeEmpty();
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
}
