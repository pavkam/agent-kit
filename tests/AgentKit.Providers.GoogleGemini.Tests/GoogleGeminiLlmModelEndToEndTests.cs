// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests;

using System.Net;
using System.Net.Http;

using AgentKit.Providers.GoogleGemini.Tests.Fakes;

/// <summary>
/// End-to-end tests for <see cref="GoogleGeminiLlmModel"/>, exercising the
/// full pipeline (capability check, credential resolution, translation,
/// transport, and response parsing) against a stub HTTP handler serving
/// fixture payloads. No test in this class performs a real network call.
/// </summary>
public sealed class GoogleGeminiLlmModelEndToEndTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static LlmModelRequest CreateRequest(
        ModelDescriptor descriptor,
        DateTimeOffset deadline,
        ImmutableArray<LlmToolDefinition> tools = default,
        ProviderRequestOptions? options = null)
    {
        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            descriptor,
            [TestMessages.User("Hello!")],
            tools.IsDefault ? [] : tools,
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);

        return new LlmModelRequest(context, attempt: 1, deadline, options ?? ProviderRequestOptions.Empty);
    }

    private static GoogleGeminiLlmModel CreateModel(
        HttpMessageHandler handler,
        IProviderCredentialSource credentials,
        ModelDescriptor? descriptor = null,
        TimeProvider? timeProvider = null,
        GoogleGeminiProviderOptions? options = null) =>
        new(
            descriptor ?? TestModels.GeminiFlash,
            options ?? new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/") },
            new GoogleGeminiContentTranslator(),
            new GoogleGeminiResponseParser(new SequentialToolCallIdGenerator()),
            credentials,
            new HttpClient(handler),
            timeProvider ?? new FakeTimeProvider(Now));

    [Fact]
    public async Task ExecuteAsync_WhenNonStreamingSuccess_SendsApiKeyHeaderAndGenerateContentUri()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://generativelanguage.test/"),
            PreferStreaming = false,
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), options: options);
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello! How can I help you today?");

        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.Method.ShouldBe(HttpMethod.Post);
        sentRequest.RequestUri.ShouldBe(
            new Uri("https://generativelanguage.test/v1beta/models/gemini-2.5-flash:generateContent"));
        sentRequest.Headers.GetValues("x-goog-api-key").ShouldContain("AIza-test");
        sentRequest.Headers.Authorization.ShouldBeNull();

        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!)!;
        sentBody.AsObject().ContainsKey("model").ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStreamingSuccess_SendsStreamGenerateContentUriAndReturnsCompletedResponse()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/streaming_text.sse", "text/event-stream");
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://generativelanguage.test/"),
            PreferStreaming = true,
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), options: options);
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");

        handler.Requests[0].RequestUri.ShouldBe(
            new Uri("https://generativelanguage.test/v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsingOAuthCredential_SendsAuthorizationBearerHeaderInsteadOfApiKey()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/"), PreferStreaming = false };
        var credential = new OAuthTokenProviderCredential("wif-token", Now.AddHours(1));
        var model = CreateModel(handler, new StaticProviderCredentialSource(credential), options: options);
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        _ = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("wif-token");
        handler.Requests[0].Headers.Contains("x-goog-api-key").ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnauthenticated_ReturnsAuthenticationFailureWithStatusCodeAndSafeMessage()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.Unauthorized, "responses/error_401.json");
        var options = new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/"), PreferStreaming = false };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-bad")), options: options);
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

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
        var options = new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/"), PreferStreaming = false };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), options: options);
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInternalServerError_ReturnsUnavailableFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.InternalServerError, "responses/error_500.json");
        var options = new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/"), PreferStreaming = false };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), options: options);
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failed.Failure.StatusCode.ShouldBe(500);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOAuthTokenExpired_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/"), PreferStreaming = false };
        var expiredCredential = new OAuthTokenProviderCredential("expired-token", Now.AddMinutes(-5));
        var model = CreateModel(handler, new StaticProviderCredentialSource(expiredCredential), options: options);
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenModelDoesNotSupportRequestedTools_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/"), PreferStreaming = false };
        var model = CreateModel(
            handler,
            new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")),
            descriptor: TestModels.NoToolSupport,
            options: options);

        var tools = ImmutableArray.Create(
            new LlmToolDefinition(new ToolId("get_weather"), "get_weather", null, JsonDocument.Parse("{}").RootElement));
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
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/"), PreferStreaming = false };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), options: options);
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddSeconds(-1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelsBeforeCredentialResolution_ReturnsCancelledResult()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/"), PreferStreaming = false };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), options: options);
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1));
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
    public async Task ExecuteAsync_WhenCancelledMidStream_ReturnsCancelledWithoutEverCompletingTheResponse()
    {
        var payload = TestResources.ReadAllBytes("responses/streaming_text.sse");
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new DelayedStream(payload)),
        });
        var options = new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), options: options);
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1));

        using var cts = new CancellationTokenSource();
        var innerObserver = new RecordingModelResponseObserver();
        var cancelingObserver = new CancelingModelResponseObserver(innerObserver, cts, cancelAfterEventCount: 1);

        var result = await model.ExecuteAsync(request, cancelingObserver, cts.Token);

        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        innerObserver.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        _ = innerObserver.Events[^1].ShouldBeOfType<ModelResponseCancelled>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderRequestOptionsCarryExtensionData_ForwardsThemInSentBody()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/"), PreferStreaming = false };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), options: options);

        var cachedContentValue = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("cachedContents/abc123")]);
        var providerOptions = new ProviderRequestOptions(
            new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("cachedContent", cachedContentValue)));
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1), options: providerOptions);
        var observer = new RecordingModelResponseObserver();

        _ = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["cachedContent"]!.GetValue<string>().ShouldBe("cachedContents/abc123");
    }
}
