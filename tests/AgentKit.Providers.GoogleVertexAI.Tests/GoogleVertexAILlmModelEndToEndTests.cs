// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Tests;

using System.Net;

using AgentKit.Providers.GoogleVertexAI.Tests.Fakes;

/// <summary>
/// End-to-end tests for the concrete <see cref="GoogleVertexAILlmModel"/>,
/// constructed directly (bypassing dependency injection) against a stub
/// HTTP handler serving fixture payloads. No test in this class performs a
/// real network call to Vertex AI.
/// </summary>
public sealed class GoogleVertexAILlmModelEndToEndTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static LlmModelRequest CreateRequest(ModelDescriptor descriptor)
    {
        var userMessage = new UserMessage(
            new MessageId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            conversationId: null,
            new BranchId(Guid.NewGuid()),
            new RunId(Guid.NewGuid()),
            new TurnId(Guid.NewGuid()),
            Now,
            MessageState.Complete,
            [new TextPart("Hi!", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            descriptor,
            [userMessage],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);

        return new LlmModelRequest(context, attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);
    }

    private static ModelDescriptor CreateDescriptor(DeploymentId? deploymentId = null) =>
        new(
            new ModelAlias("chat"),
            GoogleVertexAIProviderDefaults.ProviderId,
            GoogleVertexAIProviderDefaults.ApiFamily,
            new ModelId("gemini-2.5-flash"),
            deploymentId,
            GoogleVertexAIProviderDefaults.DefaultCapabilities,
            GoogleVertexAIProviderDefaults.DefaultLimits,
            pricing: null,
            ExtensionData.Empty);

    private static GoogleVertexAILlmModel CreateModel(
        StubHttpMessageHandler handler,
        IProviderCredentialSource credentials,
        ModelDescriptor descriptor,
        bool preferStreaming = false) =>
        new(
            descriptor,
            new GoogleVertexAIProviderOptions { ProjectId = "my-project", Location = "us-central1", PreferStreaming = preferStreaming },
            new GoogleGeminiContentTranslator(),
            new GoogleGeminiResponseParser(new SequentialToolCallIdGenerator()),
            credentials,
            new HttpClient(handler),
            new FakeTimeProvider(Now));

    [Fact]
    public async Task ExecuteAsync_WhenUsingOAuthCredential_SendsBearerHeaderAndPublisherModelUri()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticOAuthCredentialSource("gcp-access-token"), descriptor);

        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello from Vertex AI!");
        completed.Response.Identity.ProviderId.ShouldBe(GoogleVertexAIProviderDefaults.ProviderId);

        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.RequestUri.ShouldBe(new Uri(
            "https://us-central1-aiplatform.googleapis.com/v1/projects/my-project/locations/us-central1/" +
            "publishers/google/models/gemini-2.5-flash:generateContent"));
        sentRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        sentRequest.Headers.Authorization!.Parameter.ShouldBe("gcp-access-token");

        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!)!;
        sentBody.AsObject().ContainsKey("model").ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeploymentIdConfigured_UsesEndpointResourceUri()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var descriptor = CreateDescriptor(new DeploymentId("my-endpoint"));
        var model = CreateModel(handler, new StaticOAuthCredentialSource("gcp-access-token"), descriptor);

        _ = await model.ExecuteAsync(CreateRequest(descriptor), new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);

        handler.Requests[0].RequestUri.ShouldBe(new Uri(
            "https://us-central1-aiplatform.googleapis.com/v1/projects/my-project/locations/us-central1/" +
            "endpoints/my-endpoint:generateContent"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenStreamingSuccess_UsesStreamGenerateContentUri()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticOAuthCredentialSource("gcp-access-token"), descriptor, preferStreaming: true);

        _ = await model.ExecuteAsync(CreateRequest(descriptor), new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);

        handler.Requests[0].RequestUri!.ToString().ShouldContain(":streamGenerateContent?alt=sse");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsingApiKeyCredential_FailsAuthenticationWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("some-key")), descriptor);

        var result = await model.ExecuteAsync(CreateRequest(descriptor), new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsingExpiredOAuthCredential_FailsAuthenticationWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var descriptor = CreateDescriptor();
        var expiredToken = new OAuthTokenProviderCredential("expired", Now.AddMinutes(-1));
        var model = CreateModel(handler, new DelegatingOAuthCredentialSource(new StaticOAuthTokenProvider(expiredToken)), descriptor);

        var result = await model.ExecuteAsync(CreateRequest(descriptor), new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnauthenticated_ReturnsAuthenticationFailureWithStatusCodeAndSafeMessage()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.Unauthorized, "responses/error_401.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticOAuthCredentialSource("bad-token"), descriptor);

        var result = await model.ExecuteAsync(CreateRequest(descriptor), new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failed.Failure.StatusCode.ShouldBe(401);
        failed.Failure.SafeMessage.ShouldBe("Request had invalid authentication credentials.");
        failed.Failure.ProviderCode.ShouldBe("UNAUTHENTICATED");
    }

    [Fact]
    public async Task ExecuteAsync_WhenThrottled_ReturnsThrottlingFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.TooManyRequests, "responses/error_429.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticOAuthCredentialSource("gcp-access-token"), descriptor);

        var result = await model.ExecuteAsync(CreateRequest(descriptor), new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
    }

    [Fact]
    public async Task ExecuteAsync_WhenModelDoesNotSupportRequestedTools_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var descriptor = CreateDescriptor() with
        {
            Capabilities = GoogleVertexAIProviderDefaults.DefaultCapabilities with { SupportsToolCalls = false },
        };
        var model = CreateModel(handler, new StaticOAuthCredentialSource("gcp-access-token"), descriptor);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            descriptor,
            [],
            [new LlmToolDefinition(new ToolId("get_weather"), "get_weather", null, JsonDocument.Parse("{}").RootElement)],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);

        var result = await model.ExecuteAsync(request, new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeadlineAlreadyElapsed_FailsWithTimeoutWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticOAuthCredentialSource("gcp-access-token"), descriptor);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            descriptor,
            [],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, Now.AddSeconds(-1), ProviderRequestOptions.Empty);

        var result = await model.ExecuteAsync(request, new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelsBeforeCredentialResolution_ReturnsCancelledResult()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await model.ExecuteAsync(CreateRequest(descriptor), new RecordingModelResponseObserver(), cts.Token);

        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        handler.Requests.ShouldBeEmpty();
    }

    private sealed class StaticOAuthCredentialSource: IProviderCredentialSource
    {
        private readonly string _accessToken;

        public StaticOAuthCredentialSource(string accessToken) => _accessToken = accessToken;

        public ValueTask<ProviderCredential> GetCredentialAsync(ProviderId providerId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ProviderCredential>(new OAuthTokenProviderCredential(_accessToken, expiresAtUtc: null));
    }
}
