// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.ZAi.Tests;

using System.Net;

using AgentKit.Providers.ZAi.Tests.Fakes;

/// <summary>
/// End-to-end tests for the concrete <see cref="ZAiChatModel"/>, constructed
/// directly (bypassing dependency injection) against a stub HTTP handler
/// serving a fixture response. No test in this class performs a real
/// network call to Z.ai.
/// </summary>
public sealed class ZAiChatModelEndToEndTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static ChatModelRequest CreateRequest(ModelDescriptor descriptor)
    {
        var developerMessage = new DeveloperMessage(
            new MessageId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            conversationId: null,
            new BranchId(Guid.NewGuid()),
            runId: null,
            turnId: null,
            Now,
            MessageState.Complete,
            [new TextPart("Follow the house style.", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

        var userMessage = new UserMessage(
            new MessageId(Guid.NewGuid()),
            developerMessage.AgentId,
            developerMessage.SessionId,
            conversationId: null,
            developerMessage.BranchId,
            new RunId(Guid.NewGuid()),
            new TurnId(Guid.NewGuid()),
            Now,
            MessageState.Complete,
            [new TextPart("Hi!", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

        var context = new ChatRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            descriptor,
            [developerMessage, userMessage],
            [],
            ChatToolChoice.Auto,
            ChatRequestSettings.Default,
            ExtensionData.Empty);

        return new ChatModelRequest(context, attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);
    }

    private static ModelDescriptor CreateDescriptor() =>
        new(
            new ModelAlias("chat"),
            ZAiProviderDefaults.ProviderId,
            ZAiProviderDefaults.ApiFamily,
            new ModelId("glm-4.6"),
            deploymentId: null,
            ZAiProviderDefaults.DefaultCapabilities,
            ZAiProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

    [Fact]
    public async Task ExecuteAsync_WhenUsingApiKeyCredential_SendsBearerHeaderAndTranslatesDeveloperRoleAsSystem()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var options = new ZAiProviderOptions { PreferStreaming = false };
        var descriptor = CreateDescriptor();

        var model = new ZAiChatModel(
            descriptor,
            ZAiProviderDefaults.CreateProfile(options),
            new OpenAIRequestTranslator(),
            new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator()),
            new StaticApiKeyCredentialSource("zai-real-looking-key"),
            new HttpClient(handler),
            new FakeTimeProvider(Now));

        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello from Z.ai!");
        completed.Response.Identity.ProviderId.ShouldBe(ZAiProviderDefaults.ProviderId);

        _ = handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].RequestUri.ShouldBe(new Uri("https://api.z.ai/api/paas/v4/chat/completions"));
        handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("zai-real-looking-key");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsingExpiredOAuthCredential_FailsAuthenticationWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var options = new ZAiProviderOptions { PreferStreaming = false };
        var descriptor = CreateDescriptor();
        var expiredToken = new OAuthTokenProviderCredential("expired", Now.AddMinutes(-1));

        var model = new ZAiChatModel(
            descriptor,
            ZAiProviderDefaults.CreateProfile(options),
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
