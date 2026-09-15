// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MoonshotKimi.Tests;

using System.Net;

using AgentKit.Providers.MoonshotKimi.Tests.Fakes;

/// <summary>Verifies MoonshotKimiLlmModel behavior and contracts.</summary>
public sealed class MoonshotKimiLlmModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static LlmModelRequest CreateRequest(ModelDescriptor descriptor)
    {
        var systemMessage = new SystemMessage(new MessageId(Guid.NewGuid()), new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), conversationId: null, new BranchId(Guid.NewGuid()), runId: null, turnId: null, Now, MessageState.Complete, [new TextPart("You are helpful.", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var userMessage = new UserMessage(new MessageId(Guid.NewGuid()), systemMessage.AgentId, systemMessage.SessionId, conversationId: null, systemMessage.BranchId, new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()), Now, MessageState.Complete, [new TextPart("Hi!", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), descriptor, [systemMessage, userMessage], [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        return new LlmModelRequest(context, attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);
    }

    private static ModelDescriptor CreateDescriptor() => new(new ModelAlias("chat"), MoonshotKimiProviderDefaults.ProviderId, MoonshotKimiProviderDefaults.ApiFamily, new ModelId("kimi-k2-0711-preview"), deploymentId: null, MoonshotKimiProviderDefaults.DefaultCapabilities, MoonshotKimiProviderDefaults.DefaultLimits, pricing: null, ExtensionData.Empty);
    [Fact]
    public async Task ExecuteAsync_WhenUsingApiKeyCredential_SendsBearerHeaderAndReturnsCompletedResponse()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var options = new MoonshotKimiProviderOptions
        {
            PreferStreaming = false
        };
        var descriptor = CreateDescriptor();
        var model = new MoonshotKimiLlmModel(descriptor, MoonshotKimiProviderDefaults.CreateProfile(options), new OpenAIRequestTranslator(), new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator()), new StaticApiKeyCredentialSource("real-looking-key"), new HttpClient(handler), new FakeTimeProvider(Now));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello from Moonshot Kimi!");
        completed.Response.Identity.ProviderId.ShouldBe(MoonshotKimiProviderDefaults.ProviderId);
        _ = handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].RequestUri.ShouldBe(new Uri("https://api.moonshot.ai/v1/chat/completions"));
        handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("real-looking-key");
    }

    [Fact]
    public async Task ExecuteAsync_WhenHistoryReplaysReasoningAndToolCallTurn_SendsReasoningContentOnAssistantMessage()
    {
        // Arrange: turn 1 produced reasoning plus a tool call; turn 2 must replay both with the tool result.
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var options = new MoonshotKimiProviderOptions
        {
            PreferStreaming = false
        };
        var descriptor = CreateDescriptor();
        var model = new MoonshotKimiLlmModel(descriptor, MoonshotKimiProviderDefaults.CreateProfile(options), new OpenAIRequestTranslator(), new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator()), new StaticApiKeyCredentialSource("real-looking-key"), new HttpClient(handler), new FakeTimeProvider(Now));
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var branchId = new BranchId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var callId = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var toolReference = new ToolReference(new ToolId("web_search"), null, "web_search");
        var userMessage = new UserMessage(new MessageId(Guid.NewGuid()), agentId, sessionId, conversationId: null, branchId, runId, turnId, Now, MessageState.Complete, [new TextPart("Summarize today's news.", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var assistantMessage = new AssistantMessage(new MessageId(Guid.NewGuid()), agentId, sessionId, conversationId: null, branchId, runId, turnId, Now, MessageState.Complete,
            [
                new ReasoningPart(new ReasoningContent("I should search first.", ReasoningVisibility.Visible, signatureToken: null, ExtensionData.Empty), ExtensionData.Empty),
                new ToolCallPart(callId, toolReference, JsonDocument.Parse("""{"query":"news"}""").RootElement, new ProviderToolCallId("web_search:0"), ExtensionData.Empty),
            ],
            new AssistantResponseMetadata(
                new ModelRequestId(Guid.NewGuid()),
                new ProviderResponseIdentity(MoonshotKimiProviderDefaults.ProviderId, upstreamProviderId: null, MoonshotKimiProviderDefaults.ApiFamily, new ModelId("kimi-k2-0711-preview"), new ModelId("kimi-k2-0711-preview"), deploymentId: null, requestId: null, responseId: null),
                NormalizedStopReason.ToolUse,
                rawStopReason: "tool_calls",
                ModelUsage.NotReported,
                ExtensionData.Empty),
            ExtensionData.Empty);
        var toolMessage = new ToolMessage(new MessageId(Guid.NewGuid()), agentId, sessionId, conversationId: null, branchId, runId, turnId, Now, MessageState.Complete,
            [new ToolResultPart(callId, toolReference, new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty), [new TextPart("headline one", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty)],
            ExtensionData.Empty);
        var tools = ImmutableArray.Create(new LlmToolDefinition(new ToolId("web_search"), "web_search", "Searches the web.", JsonDocument.Parse("""{"type":"object","properties":{"query":{"type":"string"}}}""").RootElement));
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), descriptor, [userMessage, assistantMessage, toolMessage], tools, LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);

        // Act
        var result = await model.ExecuteAsync(request, new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);

        // Assert
        _ = result.ShouldBeOfType<ModelAttemptCompleted>();
        var sentBody = JsonNode.Parse(handler.RequestBodies.ShouldHaveSingleItem()!)!;
        var messages = sentBody["messages"]!.AsArray();
        messages.Count.ShouldBe(3);
        var assistant = messages[1]!.AsObject();
        assistant["role"]!.GetValue<string>().ShouldBe("assistant");
        assistant["reasoning_content"]!.GetValue<string>().ShouldBe("I should search first.");
        assistant["tool_calls"]!.AsArray().Single()!["id"]!.GetValue<string>().ShouldBe("web_search:0");
        messages[2]!["role"]!.GetValue<string>().ShouldBe("tool");
        messages[2]!["tool_call_id"]!.GetValue<string>().ShouldBe("web_search:0");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsingExpiredOAuthCredential_FailsAuthenticationWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var options = new MoonshotKimiProviderOptions
        {
            PreferStreaming = false
        };
        var descriptor = CreateDescriptor();
        var expiredToken = new OAuthTokenProviderCredential("expired", Now.AddMinutes(-1));
        var model = new MoonshotKimiLlmModel(descriptor, MoonshotKimiProviderDefaults.CreateProfile(options), new OpenAIRequestTranslator(), new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator()), new DelegatingOAuthCredentialSource(new StaticOAuthTokenProvider(expiredToken)), new HttpClient(handler), new FakeTimeProvider(Now));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        handler.Requests.ShouldBeEmpty();
    }

    private sealed class StaticOAuthTokenProvider: IOAuthAccessTokenProvider
    {
        private readonly OAuthTokenProviderCredential _credential;
        public StaticOAuthTokenProvider(OAuthTokenProviderCredential credential) => _credential = credential;
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_credential);
    }
}
