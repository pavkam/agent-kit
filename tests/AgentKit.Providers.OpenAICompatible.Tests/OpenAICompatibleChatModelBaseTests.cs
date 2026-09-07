// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests;

using System.Net;
using System.Net.Http;

using AgentKit.Providers.OpenAICompatible.Tests.Fakes;

/// <summary>
/// End-to-end tests for <see cref="OpenAICompatibleChatModelBase"/>,
/// exercising the full pipeline (capability check, credential resolution,
/// translation, transport, and response parsing) against a stub HTTP
/// handler serving fixture payloads. No test in this class performs a real
/// network call.
/// </summary>
public sealed class OpenAICompatibleChatModelBaseTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly OpenAICompatibilityProfile StreamingProfile = new(
        new Uri("https://api.openai.test/"),
        "v1/chat/completions",
        sendDeveloperRoleAsSystem: false,
        preferStreaming: true,
        includeStreamUsage: true,
        useMaxCompletionTokensField: true,
        []);

    private static readonly OpenAICompatibilityProfile NonStreamingProfile = StreamingProfile with { PreferStreaming = false };

    private static ChatModelRequest CreateRequest(
        ModelDescriptor descriptor,
        DateTimeOffset deadline,
        ImmutableArray<ChatToolDefinition> tools = default)
    {
        var context = new ChatRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            descriptor,
            [TestMessages.User("Hello!")],
            tools.IsDefault ? [] : tools,
            ChatToolChoice.Auto,
            ChatRequestSettings.Default,
            ExtensionData.Empty);

        return new ChatModelRequest(context, attempt: 1, deadline, ProviderRequestOptions.Empty);
    }

    private static TestChatModel CreateModel(
        HttpMessageHandler handler,
        OpenAICompatibilityProfile profile,
        IProviderCredentialSource credentials,
        ModelDescriptor? descriptor = null,
        TimeProvider? timeProvider = null) =>
        new(
            descriptor ?? TestModels.Gpt4O,
            profile,
            new OpenAIRequestTranslator(),
            new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator()),
            credentials,
            new HttpClient(handler),
            timeProvider ?? new FakeTimeProvider(Now));

    [Fact]
    public async Task ExecuteAsync_WhenNonStreamingSuccess_ReturnsCompletedResponseWithAuthorizationHeader()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_success.json");
        var model = CreateModel(handler, NonStreamingProfile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var request = CreateRequest(TestModels.Gpt4O, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello! How can I help you today?");

        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.Method.ShouldBe(HttpMethod.Post);
        sentRequest.RequestUri.ShouldBe(new Uri("https://api.openai.test/v1/chat/completions"));
        sentRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        sentRequest.Headers.Authorization!.Parameter.ShouldBe("sk-test");

        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["stream"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStreamingSuccess_ReturnsCompletedResponse()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/streaming_success.sse", "text/event-stream");
        var model = CreateModel(handler, StreamingProfile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var request = CreateRequest(TestModels.Gpt4O, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");

        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["stream"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnauthorized_ReturnsAuthenticationFailureWithStatusCodeAndSafeMessage()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.Unauthorized, "responses/error_401.json");
        var model = CreateModel(handler, NonStreamingProfile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-bad")));
        var request = CreateRequest(TestModels.Gpt4O, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failed.Failure.StatusCode.ShouldBe(401);
        failed.Failure.SafeMessage.ShouldBe("Incorrect API key provided.");
        failed.Failure.ProviderCode.ShouldBe("invalid_api_key");
    }

    [Fact]
    public async Task ExecuteAsync_WhenThrottled_ReturnsThrottlingFailureWithRetryAfter()
    {
        var handler = StubHttpMessageHandler.FromFixture(
            HttpStatusCode.TooManyRequests,
            "responses/error_429.json",
            configureHeaders: response => response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30)));
        var model = CreateModel(handler, NonStreamingProfile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var request = CreateRequest(TestModels.Gpt4O, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
        failed.Failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ExecuteAsync_WhenOAuthTokenExpired_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_success.json");
        var expiredCredential = new OAuthTokenProviderCredential("expired-token", Now.AddMinutes(-5));
        var model = CreateModel(handler, NonStreamingProfile, new StaticProviderCredentialSource(expiredCredential));
        var request = CreateRequest(TestModels.Gpt4O, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenModelDoesNotSupportRequestedTools_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_success.json");
        var model = CreateModel(
            handler,
            NonStreamingProfile,
            new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")),
            descriptor: TestModels.NoToolSupport);

        var tools = ImmutableArray.Create(
            new ChatToolDefinition(new ToolId("get_weather"), "get_weather", null, JsonDocument.Parse("{}").RootElement));
        var request = CreateRequest(TestModels.NoToolSupport, Now.AddMinutes(1), tools);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeadlineAlreadyElapsed_FailsWithTimeoutWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_success.json");
        var model = CreateModel(handler, NonStreamingProfile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var request = CreateRequest(TestModels.Gpt4O, Now.AddSeconds(-1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelsBeforeCredentialResolution_ReturnsCancelledResult()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_success.json");
        var model = CreateModel(handler, NonStreamingProfile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var request = CreateRequest(TestModels.Gpt4O, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await model.ExecuteAsync(request, observer, cts.Token);

        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        handler.Requests.ShouldBeEmpty();
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseCancelled>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnsupportedContentIsTranslated_FailsWithInvalidRequestWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_success.json");
        var model = CreateModel(handler, NonStreamingProfile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));

        var mediaPart = new MediaReferencePart(
            new MediaReference(
                new MediaId(Guid.NewGuid()),
                MediaSourceKind.Uri,
                "image/png",
                new Uri("https://example.com/image.png"),
                [],
                sizeInBytes: null,
                hash: null,
                ExtensionData.Empty),
            MediaSemantics.Input,
            ExtensionData.Empty);

        var context = new ChatRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.Gpt4O,
            [TestMessages.User(mediaPart)],
            [],
            ChatToolChoice.Auto,
            ChatRequestSettings.Default,
            ExtensionData.Empty);
        var request = new ChatModelRequest(context, attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenServerError_ReturnsUnavailableFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.InternalServerError, "responses/error_500.json");
        var model = CreateModel(handler, NonStreamingProfile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var request = CreateRequest(TestModels.Gpt4O, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failed.Failure.StatusCode.ShouldBe(500);
    }

    [Fact]
    public async Task ExecuteAsync_WhenThrottledWithHttpDateRetryAfter_ComputesDeltaFromCurrentTime()
    {
        var retryAfterDate = Now.AddSeconds(45);
        var handler = StubHttpMessageHandler.FromFixture(
            HttpStatusCode.TooManyRequests,
            "responses/error_429.json",
            configureHeaders: response => response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfterDate));
        var model = CreateModel(handler, NonStreamingProfile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var request = CreateRequest(TestModels.Gpt4O, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public async Task ExecuteAsync_WhenErrorBodyIsNotJson_FallsBackToGenericSafeMessage()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>Bad Gateway</html>", Encoding.UTF8, "text/html"),
        });
        var model = CreateModel(handler, NonStreamingProfile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var request = CreateRequest(TestModels.Gpt4O, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failed.Failure.SafeMessage.ShouldBe("The provider returned HTTP status 502.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseHasNoRequestIdHeader_LeavesProviderRequestIdNull()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_success.json");
        var model = CreateModel(handler, NonStreamingProfile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var request = CreateRequest(TestModels.Gpt4O, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Identity.RequestId.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderRequestOptionsCarryExtensionData_ForwardsThemInSentBody()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_success.json");
        var model = CreateModel(handler, NonStreamingProfile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));

        var context = new ChatRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.Gpt4O,
            [TestMessages.User("Hello!")],
            [],
            ChatToolChoice.Auto,
            ChatRequestSettings.Default,
            ExtensionData.Empty);

        var userIdValue = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("end-user-42")]);
        var options = new ProviderRequestOptions(
            new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("user", userIdValue)));
        var request = new ChatModelRequest(context, attempt: 1, Now.AddMinutes(1), options);
        var observer = new RecordingModelResponseObserver();

        _ = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["user"]!.GetValue<string>().ShouldBe("end-user-42");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledMidStream_ReturnsCancelledWithoutEverCompletingTheResponse()
    {
        var payload = TestResources.ReadAllBytes("responses/streaming_success.sse");
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new DelayedStream(payload)),
        });
        var model = CreateModel(handler, StreamingProfile, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-test")));
        var request = CreateRequest(TestModels.Gpt4O, Now.AddMinutes(1));

        using var cts = new CancellationTokenSource();
        var innerObserver = new RecordingModelResponseObserver();
        var cancelingObserver = new CancelingModelResponseObserver(innerObserver, cts, cancelAfterEventCount: 1);

        var result = await model.ExecuteAsync(request, cancelingObserver, cts.Token);

        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        innerObserver.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        _ = innerObserver.Events[^1].ShouldBeOfType<ModelResponseCancelled>();
    }
}
