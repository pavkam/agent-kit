// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI.Tests;

using System.Net;

using AgentKit.Providers.OpenAI.Tests.Fakes;

/// <summary>
/// End-to-end tests for the concrete <see cref="OpenAIChatModel"/>,
/// constructed directly (bypassing dependency injection) against a stub
/// HTTP handler serving a fixture response. No test in this class performs
/// a real network call to OpenAI.
/// </summary>
public sealed class OpenAIChatModelEndToEndTests
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
            OpenAIProviderDefaults.ProviderId,
            OpenAIProviderDefaults.ApiFamily,
            new ModelId("gpt-4o"),
            deploymentId: null,
            OpenAIProviderDefaults.DefaultCapabilities,
            OpenAIProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

    [Fact]
    public async Task ExecuteAsync_WhenUsingApiKeyCredential_SendsBearerHeaderAndReturnsCompletedResponse()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var options = new OpenAIProviderOptions { PreferStreaming = false };
        var descriptor = CreateDescriptor();

        var model = new OpenAIChatModel(
            descriptor,
            OpenAIProviderDefaults.CreateProfile(options),
            new OpenAIRequestTranslator(),
            new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator()),
            new StaticApiKeyCredentialSource("sk-real-looking-key"),
            new HttpClient(handler),
            new FakeTimeProvider(Now));

        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello from OpenAI!");
        completed.Response.Identity.ProviderId.ShouldBe(OpenAIProviderDefaults.ProviderId);

        _ = handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("sk-real-looking-key");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsingExpiredOAuthCredential_FailsAuthenticationWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var options = new OpenAIProviderOptions { PreferStreaming = false };
        var descriptor = CreateDescriptor();
        var expiredToken = new OAuthTokenProviderCredential("expired", Now.AddMinutes(-1));

        var model = new OpenAIChatModel(
            descriptor,
            OpenAIProviderDefaults.CreateProfile(options),
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

    [Fact]
    public async Task ExecuteAsync_WhenUsingValidOAuthCredential_SendsBearerHeaderAndReturnsCompletedResponse()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var options = new OpenAIProviderOptions { PreferStreaming = false };
        var descriptor = CreateDescriptor();
        var validToken = new OAuthTokenProviderCredential("valid-oauth-token", Now.AddHours(1));

        var model = new OpenAIChatModel(
            descriptor,
            OpenAIProviderDefaults.CreateProfile(options),
            new OpenAIRequestTranslator(),
            new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator()),
            new DelegatingOAuthCredentialSource(new StaticOAuthTokenProvider(validToken)),
            new HttpClient(handler),
            new FakeTimeProvider(Now));

        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ModelAttemptCompleted>();
        handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("valid-oauth-token");
    }
}
