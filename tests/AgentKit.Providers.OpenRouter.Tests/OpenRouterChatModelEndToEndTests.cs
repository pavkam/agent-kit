// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter.Tests;

using System.Net;

using AgentKit.Providers.OpenRouter.Tests.Fakes;

/// <summary>
/// End-to-end tests for the concrete <see cref="OpenRouterChatModel"/>,
/// constructed directly (bypassing dependency injection) against a stub
/// HTTP handler serving a fixture response. No test in this class performs
/// a real network call to OpenRouter.
/// </summary>
public sealed class OpenRouterChatModelEndToEndTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static ChatModelRequest CreateRequest(ModelDescriptor descriptor)
    {
        var systemMessage = new SystemMessage(
            new MessageId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            conversationId: null,
            new BranchId(Guid.NewGuid()),
            runId: null,
            turnId: null,
            Now,
            MessageState.Complete,
            [new TextPart("You are helpful.", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

        var userMessage = new UserMessage(
            new MessageId(Guid.NewGuid()),
            systemMessage.AgentId,
            systemMessage.SessionId,
            conversationId: null,
            systemMessage.BranchId,
            new RunId(Guid.NewGuid()),
            new TurnId(Guid.NewGuid()),
            Now,
            MessageState.Complete,
            [new TextPart("Hi!", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

        var context = new ChatRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            descriptor,
            [systemMessage, userMessage],
            [],
            ChatToolChoice.Auto,
            ChatRequestSettings.Default,
            ExtensionData.Empty);

        return new ChatModelRequest(context, attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);
    }

    private static ModelDescriptor CreateDescriptor() =>
        new(
            new ModelAlias("chat"),
            OpenRouterProviderDefaults.ProviderId,
            OpenRouterProviderDefaults.ApiFamily,
            new ModelId("openai/gpt-4o"),
            deploymentId: null,
            OpenRouterProviderDefaults.DefaultCapabilities,
            OpenRouterProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

    [Fact]
    public async Task ExecuteAsync_WhenUsingApiKeyCredential_SendsBearerHeaderAndAttributionHeaders()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var options = new OpenRouterProviderOptions
        {
            PreferStreaming = false,
            HttpReferer = "https://example.test/",
            ApplicationTitle = "AgentKit Tests",
        };
        var descriptor = CreateDescriptor();

        var model = new OpenRouterChatModel(
            descriptor,
            OpenRouterProviderDefaults.CreateProfile(options),
            new OpenAIRequestTranslator(),
            new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator()),
            new StaticApiKeyCredentialSource("sk-or-real-looking-key"),
            new HttpClient(handler),
            new FakeTimeProvider(Now));

        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello from OpenRouter!");
        completed.Response.Identity.ProviderId.ShouldBe(OpenRouterProviderDefaults.ProviderId);

        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.RequestUri.ShouldBe(new Uri("https://openrouter.ai/api/v1/chat/completions"));
        sentRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        sentRequest.Headers.Authorization!.Parameter.ShouldBe("sk-or-real-looking-key");
        sentRequest.Headers.GetValues("HTTP-Referer").ShouldContain("https://example.test/");
        sentRequest.Headers.GetValues("X-OpenRouter-Title").ShouldContain("AgentKit Tests");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsingExpiredOAuthCredential_FailsAuthenticationWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var options = new OpenRouterProviderOptions { PreferStreaming = false };
        var descriptor = CreateDescriptor();
        var expiredToken = new OAuthTokenProviderCredential("expired", Now.AddMinutes(-1));

        var model = new OpenRouterChatModel(
            descriptor,
            OpenRouterProviderDefaults.CreateProfile(options),
            new OpenAIRequestTranslator(),
            new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator()),
            new DelegatingOAuthCredentialSource(new StaticOAuthTokenProvider(expiredToken)),
            new HttpClient(handler),
            new FakeTimeProvider(Now));

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

        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_credential);
    }
}
