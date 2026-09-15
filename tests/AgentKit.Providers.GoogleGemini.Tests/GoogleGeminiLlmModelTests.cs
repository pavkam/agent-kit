// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using AgentKit.Providers.GoogleGemini.Tests.Fakes;

/// <summary>Verifies GoogleGeminiLlmModel behavior and contracts.</summary>
public sealed class GoogleGeminiLlmModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static LlmModelRequest CreateRequest(ModelDescriptor descriptor, DateTimeOffset deadline, ImmutableArray<LlmToolDefinition> tools = default, ProviderRequestOptions? options = null)
    {
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), descriptor, [TestMessages.User("Hello!")], tools.IsDefault ? [] : tools, LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        return new LlmModelRequest(context, attempt: 1, deadline, options ?? ProviderRequestOptions.Empty);
    }

    private static GoogleGeminiLlmModel CreateModel(HttpMessageHandler handler, IProviderCredentialSource credentials, ModelDescriptor? descriptor = null, TimeProvider? timeProvider = null, GoogleGeminiProviderOptions? options = null) => new(descriptor ?? TestModels.GeminiFlash, options ?? new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/") }, new GoogleGeminiContentTranslator(), new GoogleGeminiResponseParser(new SequentialToolCallIdGenerator()), credentials, new HttpClient(handler), timeProvider ?? new FakeTimeProvider(Now));
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
        sentRequest.RequestUri.ShouldBe(new Uri("https://generativelanguage.test/v1beta/models/gemini-2.5-flash:generateContent"));
        sentRequest.Headers.GetValues("x-goog-api-key").ShouldContain("AIza-test");
        sentRequest.Headers.Authorization.ShouldBeNull();
        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!)!;
        sentBody.AsObject().ContainsKey("model").ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStreamingSuccess_EmitsExactlyOneResponseStartedWithContiguousSequences()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/streaming_text.sse", "text/event-stream");
        var options = new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), options: options);
        var observer = new RecordingModelResponseObserver();

        _ = await model.ExecuteAsync(CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        observer.Events.OfType<ModelResponseStarted>().Count().ShouldBe(1);
        observer.Events.Select(static e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(static i => (long) i));
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
        handler.Requests[0].RequestUri.ShouldBe(new Uri("https://generativelanguage.test/v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsingOAuthCredential_SendsAuthorizationBearerHeaderInsteadOfApiKey()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://generativelanguage.test/"),
            PreferStreaming = false
        };
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
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://generativelanguage.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-bad")), options: options);
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failed.Failure.StatusCode.ShouldBe(401);
        failed.Failure.SafeMessage.ShouldBe("The Google Gemini request failed with HTTP status 401.");
        failed.Failure.ProviderCode.ShouldBe("UNAUTHENTICATED");
    }

    [Fact]
    public async Task ExecuteAsync_WhenThrottled_ReturnsThrottlingFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.TooManyRequests, "responses/error_429.json");
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://generativelanguage.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), options: options);
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
    }

    [Fact]
    public async Task ExecuteAsync_WhenThrottledWithHttpDateRetryAfter_ComputesDeltaFromCurrentTime()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(File.ReadAllText(TestResources.GetPath("responses/error_429.json")), Encoding.UTF8, "application/json"),
            };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(Now.AddSeconds(45));
            return response;
        });
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://generativelanguage.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), options: options);
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
        failed.Failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public async Task ExecuteAsync_WhenInternalServerError_ReturnsUnavailableFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.InternalServerError, "responses/error_500.json");
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://generativelanguage.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), options: options);
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failed.Failure.StatusCode.ShouldBe(500);
    }

    /// <summary>Verifies non-success status families always produce typed failures without redirect or retry effects.</summary>
    [Theory]
    [InlineData(307, ProviderFailureKind.ProtocolViolation)]
    [InlineData(408, ProviderFailureKind.Timeout)]
    [InlineData(409, ProviderFailureKind.InvalidRequest)]
    [InlineData(502, ProviderFailureKind.Unavailable)]
    [InlineData(504, ProviderFailureKind.Timeout)]
    [InlineData(599, ProviderFailureKind.Unavailable)]
    [InlineData(600, ProviderFailureKind.Unknown)]
    public async Task ExecuteAsync_WhenErrorStatusHasMalformedBody_UsesHttpFallback(int statusCode, ProviderFailureKind expected)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage((HttpStatusCode) statusCode) { Content = new StringContent("not-json") });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), options: new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/"), PreferStreaming = false });
        var result = await model.ExecuteAsync(CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1)), new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);
        var failure = result.ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(expected);
        failure.StatusCode.ShouldBe(statusCode);
        _ = failure.DiagnosticCause.ShouldBeOfType<JsonException>();
        handler.Requests.Count.ShouldBe(1);
    }

    /// <summary>Verifies caller cancellation while reading an error body returns one cancellation outcome.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelsDuringErrorBodyRead_ReturnsCancelled()
    {
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StreamContent(body)
            };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(17));
            return response;
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")));
        using var cancellation = new CancellationTokenSource();
        var pending = model.ExecuteAsync(CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1)), new RecordingModelResponseObserver(), cancellation.Token);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        await body.Entered;
        await cancellation.CancelAsync();
        var result = await pending;
        var failure = result.ShouldBeOfType<ModelAttemptCancelled>().Cancellation;
        failure.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        failure.StatusCode.ShouldBe(400);
        failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(17));
    }

    /// <summary>Verifies the injected deadline cancels a blocked error-body read without wall-clock waiting.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenDeadlineExpiresDuringErrorBodyRead_ReturnsTimeout()
    {
        var clock = new FakeTimeProvider(Now);
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StreamContent(body)
            };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(23));
            return response;
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), timeProvider: clock);
        var pending = model.ExecuteAsync(CreateRequest(TestModels.GeminiFlash, Now.AddSeconds(1)), new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        await body.Entered;
        clock.Advance(TimeSpan.FromSeconds(2));
        var failure = (await pending).ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failure.StatusCode.ShouldBe(400);
        failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(23));
    }

    /// <summary>Verifies observer cancellation during failure publication cannot trigger a second terminal event.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenFailureObserverThrowsCancellation_DoesNotPublishSecondTerminalEvent()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")));
        using var cancellation = new CancellationTokenSource();
        var observer = new CancellingFailureObserver(cancellation);
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await model.ExecuteAsync(CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1)), observer, cancellation.Token));
        observer.TerminalEventCount.ShouldBe(1);
        cancellation.IsCancellationRequested.ShouldBeTrue();
    }

    /// <summary>Verifies a missing error body still produces a bounded typed failure.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenErrorBodyIsAbsent_ReturnsTypedFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict));
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")));
        var failure = (await model.ExecuteAsync(CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1)), new RecordingModelResponseObserver(), TestContext.Current.CancellationToken)).ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failure.SafeMessage.ShouldBe("The Google Gemini request failed with HTTP status 409.");
    }

    /// <summary>Verifies unknown Google status text retains its code while HTTP classifies it and body text remains unsafe.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenBodyStatusIsUnknown_UsesHttpFallbackWithoutLeakingMessage()
    {
        const string secret = "token=do-not-leak";
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(JsonSerializer.Serialize(new { error = new { code = 400, status = "FUTURE_STATUS", message = secret } })), });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), options: new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/"), PreferStreaming = false });
        var failure = (await model.ExecuteAsync(CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1)), new RecordingModelResponseObserver(), TestContext.Current.CancellationToken)).ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failure.ProviderCode.ShouldBe("FUTURE_STATUS");
        failure.SafeMessage.ShouldNotContain(secret);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOAuthTokenExpired_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://generativelanguage.test/"),
            PreferStreaming = false
        };
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
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://generativelanguage.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), descriptor: TestModels.NoToolSupport, options: options);
        var tools = ImmutableArray.Create(new LlmToolDefinition(new ToolId("get_weather"), "get_weather", null, JsonDocument.Parse("{}").RootElement));
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
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://generativelanguage.test/"),
            PreferStreaming = false
        };
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
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://generativelanguage.test/"),
            PreferStreaming = false
        };
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
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new DelayedStream(payload)), });
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://generativelanguage.test/"),
            PreferStreaming = true
        };
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
        var options = new GoogleGeminiProviderOptions
        {
            BaseAddress = new Uri("https://generativelanguage.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("AIza-test")), options: options);
        var cachedContentValue = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("cachedContents/abc123")]);
        var providerOptions = new ProviderRequestOptions(new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("cachedContent", cachedContentValue)));
        var request = CreateRequest(TestModels.GeminiFlash, Now.AddMinutes(1), options: providerOptions);
        var observer = new RecordingModelResponseObserver();
        _ = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["cachedContent"]!.GetValue<string>().ShouldBe("cachedContents/abc123");
    }
}
