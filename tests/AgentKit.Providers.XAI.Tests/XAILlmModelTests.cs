// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.XAI.Tests;

using System.Net;

using AgentKit.Providers.XAI.Tests.Fakes;

/// <summary>Verifies XAILlmModel behavior and contracts.</summary>
public sealed class XAILlmModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static LlmModelRequest CreateRequest(ModelDescriptor descriptor)
    {
        var systemMessage = new SystemMessage(new MessageId(Guid.NewGuid()), new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), conversationId: null, new BranchId(Guid.NewGuid()), runId: null, turnId: null, Now, MessageState.Complete, [new TextPart("You are helpful.", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var userMessage = new UserMessage(new MessageId(Guid.NewGuid()), systemMessage.AgentId, systemMessage.SessionId, conversationId: null, systemMessage.BranchId, new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()), Now, MessageState.Complete, [new TextPart("Hi!", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), descriptor, [systemMessage, userMessage], [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        return new LlmModelRequest(context, attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);
    }

    private static ModelDescriptor CreateDescriptor() => new(new ModelAlias("chat"), XAIProviderDefaults.ProviderId, XAIProviderDefaults.ApiFamily, new ModelId("grok-4"), deploymentId: null, XAIProviderDefaults.DefaultCapabilities, XAIProviderDefaults.DefaultLimits, pricing: null, ExtensionData.Empty);
    [Fact]
    public async Task ExecuteAsync_WhenUsingApiKeyCredential_SendsBearerHeaderAndReturnsCompletedResponse()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var options = new XAIProviderOptions
        {
            PreferStreaming = false
        };
        var descriptor = CreateDescriptor();
        var model = new XAILlmModel(descriptor, XAIProviderDefaults.CreateProfile(options), new OpenAIRequestTranslator(), new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator()), new StaticApiKeyCredentialSource("real-looking-key"), new HttpClient(handler), new FakeTimeProvider(Now));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello from xAI!");
        completed.Response.Identity.ProviderId.ShouldBe(XAIProviderDefaults.ProviderId);
        _ = handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].RequestUri.ShouldBe(new Uri("https://api.x.ai/v1/chat/completions"));
        handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("real-looking-key");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsingExpiredOAuthCredential_FailsAuthenticationWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var options = new XAIProviderOptions
        {
            PreferStreaming = false
        };
        var descriptor = CreateDescriptor();
        var expiredToken = new OAuthTokenProviderCredential("expired", Now.AddMinutes(-1));
        var model = new XAILlmModel(descriptor, XAIProviderDefaults.CreateProfile(options), new OpenAIRequestTranslator(), new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator()), new DelegatingOAuthCredentialSource(new StaticOAuthTokenProvider(expiredToken)), new HttpClient(handler), new FakeTimeProvider(Now));
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
