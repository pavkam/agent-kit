// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI.Tests;

using System.Net;

using AgentKit.Providers.AzureOpenAI.Tests.Fakes;

/// <summary>Verifies AzureOpenAILlmModel behavior and contracts.</summary>
public sealed class AzureOpenAILlmModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static LlmModelRequest CreateRequest(ModelDescriptor descriptor)
    {
        var systemMessage = new SystemMessage(new MessageId(Guid.NewGuid()), new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), conversationId: null, new BranchId(Guid.NewGuid()), runId: null, turnId: null, Now, MessageState.Complete, [new TextPart("You are helpful.", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var userMessage = new UserMessage(new MessageId(Guid.NewGuid()), systemMessage.AgentId, systemMessage.SessionId, conversationId: null, systemMessage.BranchId, new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()), Now, MessageState.Complete, [new TextPart("Hi!", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), descriptor, [systemMessage, userMessage], [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        return new LlmModelRequest(context, attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);
    }

    private static ModelDescriptor CreateDescriptor() => new(new ModelAlias("chat"), AzureOpenAIProviderDefaults.ProviderId, AzureOpenAIProviderDefaults.ApiFamily, new ModelId("gpt-4o"), new DeploymentId("prod-gpt4o"), AzureOpenAIProviderDefaults.DefaultCapabilities, AzureOpenAIProviderDefaults.DefaultLimits, pricing: null, ExtensionData.Empty);
    private static AzureOpenAILlmModel CreateModel(StubHttpMessageHandler handler, IProviderCredentialSource credentials, ModelDescriptor descriptor) => new(descriptor, AzureOpenAIProviderDefaults.CreateProfile(new AzureOpenAIProviderOptions { ResourceEndpoint = new Uri("https://my-resource.openai.azure.test/"), PreferStreaming = false, }), new OpenAIRequestTranslator(), new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator()), credentials, new HttpClient(handler), new FakeTimeProvider(Now));
    [Fact]
    public async Task ExecuteAsync_WhenUsingApiKeyCredential_SendsApiKeyHeaderAndOverridesModelFieldWithDeploymentName()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello from Azure OpenAI!");
        completed.Response.Identity.ProviderId.ShouldBe(AzureOpenAIProviderDefaults.ProviderId);
        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.RequestUri.ShouldBe(new Uri("https://my-resource.openai.azure.test/openai/v1/chat/completions"));
        sentRequest.Headers.Contains("api-key").ShouldBeTrue();
        sentRequest.Headers.GetValues("api-key").ShouldContain("azure-resource-key");
        sentRequest.Headers.Authorization.ShouldBeNull();
        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["model"]!.GetValue<string>().ShouldBe("prod-gpt4o");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsingExpiredOAuthCredential_FailsAuthenticationWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var descriptor = CreateDescriptor();
        var expiredToken = new OAuthTokenProviderCredential("expired", Now.AddMinutes(-1));
        var model = CreateModel(handler, new DelegatingOAuthCredentialSource(new StaticOAuthTokenProvider(expiredToken)), descriptor);
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsingValidOAuthCredential_SendsBearerHeaderInsteadOfApiKey()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var descriptor = CreateDescriptor();
        var validToken = new OAuthTokenProviderCredential("valid-entra-token", Now.AddHours(1));
        var model = CreateModel(handler, new DelegatingOAuthCredentialSource(new StaticOAuthTokenProvider(validToken)), descriptor);
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<ModelAttemptCompleted>();
        handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("valid-entra-token");
        handler.Requests[0].Headers.Contains("api-key").ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnauthorized_ReturnsAuthenticationFailureWithStatusCodeAndSafeMessage()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.Unauthorized, "responses/error_401.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("bad-key"), descriptor);
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failed.Failure.StatusCode.ShouldBe(401);
        failed.Failure.SafeMessage.ShouldBe("Access denied due to invalid subscription key or wrong API endpoint.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenThrottled_ReturnsThrottlingFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.TooManyRequests, "responses/error_429.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
    }

    [Fact]
    public async Task ExecuteAsync_WhenModelDoesNotSupportRequestedTools_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var descriptor = CreateDescriptor() with
        {
            Capabilities = AzureOpenAIProviderDefaults.DefaultCapabilities with
            {
                SupportsToolCalls = false
            },
        };
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), descriptor, [], [new LlmToolDefinition(new ToolId("get_weather"), "get_weather", null, JsonDocument.Parse("{}").RootElement)], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeadlineAlreadyElapsed_FailsWithTimeoutWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), descriptor, [], [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, Now.AddSeconds(-1), ProviderRequestOptions.Empty);
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelsBeforeCredentialResolution_ReturnsCancelledResult()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("azure-resource-key")), descriptor);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, cts.Token);
        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        handler.Requests.ShouldBeEmpty();
    }
}
