// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Tests;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using AgentKit.Providers.Anthropic.Tests.Fakes;
using AgentKit.TestSupport;

/// <summary>Verifies AnthropicLlmModel behavior and contracts.</summary>
public sealed class AnthropicLlmModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static LlmModelRequest CreateRequest(ModelDescriptor descriptor, DateTimeOffset deadline, ImmutableArray<LlmToolDefinition> tools = default, ProviderRequestOptions? options = null, LlmRequestSettings? settings = null)
    {
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), descriptor, [TestMessages.User("Hello!")], tools.IsDefault ? [] : tools, LlmToolChoice.Auto, settings ?? LlmRequestSettings.Default, ExtensionData.Empty);
        return new LlmModelRequest(context, attempt: 1, deadline, options ?? ProviderRequestOptions.Empty);
    }

    private static AnthropicLlmModel CreateModel(HttpMessageHandler handler, IProviderCredentialSource credentials, ModelDescriptor? descriptor = null, TimeProvider? timeProvider = null, AnthropicProviderOptions? options = null) => new(descriptor ?? TestModels.ClaudeSonnet, options ?? new AnthropicProviderOptions { BaseAddress = new Uri("https://api.anthropic.test/") }, new AnthropicMessageTranslator(), new AnthropicMessageStreamParser(new SequentialToolCallIdGenerator()), credentials, new HttpClient(handler), timeProvider ?? new FakeTimeProvider(Now));
    [Fact]
    public async Task ExecuteAsync_WhenNonStreamingSuccess_SendsApiKeyAndVersionHeaders()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new AnthropicProviderOptions
        {
            BaseAddress = new Uri("https://api.anthropic.test/"),
            PreferStreaming = false,
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello! How can I help you today?");
        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.Method.ShouldBe(HttpMethod.Post);
        sentRequest.RequestUri.ShouldBe(new Uri("https://api.anthropic.test/v1/messages"));
        sentRequest.Headers.GetValues("x-api-key").ShouldContain("sk-ant-test");
        sentRequest.Headers.GetValues("anthropic-version").ShouldContain(AnthropicProviderDefaults.DefaultAnthropicVersion);
        sentRequest.Headers.Authorization.ShouldBeNull();
        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["stream"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStreamingSuccess_ReturnsCompletedResponse()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/streaming_text.sse", "text/event-stream");
        var options = new AnthropicProviderOptions
        {
            BaseAddress = new Uri("https://api.anthropic.test/"),
            PreferStreaming = true,
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");
        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["stream"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStreamingSuccess_EmitsExactlyOneResponseStartedWithContiguousSequences()
    {
        // streaming-and-event-protocol.md: exactly one ResponseStarted per attempt; sequences are strictly increasing.
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/streaming_text.sse", "text/event-stream");
        var options = new AnthropicProviderOptions { BaseAddress = new Uri("https://api.anthropic.test/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: options);
        var observer = new RecordingModelResponseObserver();

        _ = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        observer.Events.OfType<ModelResponseStarted>().Count().ShouldBe(1);
        observer.Events.Select(static e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(static i => (long) i));
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsingOAuthCredential_SendsAuthorizationBearerHeaderInsteadOfApiKey()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new AnthropicProviderOptions
        {
            BaseAddress = new Uri("https://api.anthropic.test/"),
            PreferStreaming = false
        };
        var credential = new OAuthTokenProviderCredential("wif-token", Now.AddHours(1));
        var model = CreateModel(handler, new StaticProviderCredentialSource(credential), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        _ = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("wif-token");
        handler.Requests[0].Headers.Contains("x-api-key").ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnauthorized_ReturnsAuthenticationFailureWithStatusCodeAndSafeMessage()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.Unauthorized, "responses/error_401.json");
        var options = new AnthropicProviderOptions
        {
            BaseAddress = new Uri("https://api.anthropic.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-bad")), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failed.Failure.StatusCode.ShouldBe(401);
        failed.Failure.SafeMessage.ShouldBe("The Anthropic request failed with HTTP status 401.");
        failed.Failure.ProviderCode.ShouldBe("authentication_error");
    }

    [Fact]
    public async Task ExecuteAsync_WhenThrottled_ReturnsThrottlingFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.TooManyRequests, "responses/error_429.json");
        var options = new AnthropicProviderOptions
        {
            BaseAddress = new Uri("https://api.anthropic.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
    }

    [Fact]
    public async Task ExecuteAsync_WhenThrottledWithHttpDateRetryAfter_ComputesDeltaFromCurrentTime()
    {
        var handler = StubHttpMessageHandler.FromFixture(
            HttpStatusCode.TooManyRequests,
            "responses/error_429.json",
            configureHeaders: response => response.Headers.RetryAfter = new RetryConditionHeaderValue(Now.AddSeconds(45)));
        var options = new AnthropicProviderOptions
        {
            BaseAddress = new Uri("https://api.anthropic.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
        failed.Failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public async Task ExecuteAsync_WhenOverloaded529_ReturnsUnavailableFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture((HttpStatusCode) 529, "responses/error_529.json");
        var options = new AnthropicProviderOptions
        {
            BaseAddress = new Uri("https://api.anthropic.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failed.Failure.StatusCode.ShouldBe(529);
    }

    /// <summary>Verifies non-success status families always produce a typed failure without following redirects.</summary>
    [Theory]
    [InlineData(302, ProviderFailureKind.ProtocolViolation)]
    [InlineData(408, ProviderFailureKind.Timeout)]
    [InlineData(409, ProviderFailureKind.InvalidRequest)]
    [InlineData(413, ProviderFailureKind.InvalidRequest)]
    [InlineData(503, ProviderFailureKind.Unavailable)]
    [InlineData(504, ProviderFailureKind.Timeout)]
    [InlineData(599, ProviderFailureKind.Unavailable)]
    [InlineData(600, ProviderFailureKind.Unknown)]
    public async Task ExecuteAsync_WhenErrorStatusHasNoUsableBody_UsesHttpFallback(int statusCode, ProviderFailureKind expected)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage((HttpStatusCode) statusCode) { Content = new StringContent("not-json") });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: new AnthropicProviderOptions { BaseAddress = new Uri("https://api.anthropic.test/"), PreferStreaming = false });
        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);
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
            response.Headers.Add("request-id", "req_cancelled");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(17));
            return response;
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")));
        using var cancellation = new CancellationTokenSource();
        var pending = model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), new RecordingModelResponseObserver(), cancellation.Token);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        await body.Entered;
        await cancellation.CancelAsync();
        var result = await pending;
        var failure = result.ShouldBeOfType<ModelAttemptCancelled>().Cancellation;
        failure.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        failure.StatusCode.ShouldBe(400);
        failure.RequestId.ShouldBe(new ProviderRequestId("req_cancelled"));
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
            response.Headers.Add("request-id", "req_timeout");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(23));
            return response;
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), timeProvider: clock);
        var pending = model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddSeconds(1)), new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        await body.Entered;
        clock.Advance(TimeSpan.FromSeconds(2));
        var failure = (await pending).ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failure.StatusCode.ShouldBe(400);
        failure.RequestId.ShouldBe(new ProviderRequestId("req_timeout"));
        failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(23));
    }

    /// <summary>Verifies a connection fault while reading an error body still yields the status-mapped failure with the fault retained as diagnostics.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenErrorBodyReadFails_ReturnsStatusOnlyFailure()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StreamContent(FaultingReadStream.ConnectionReset("{\"error\":"u8.ToArray()))
            };
            response.Headers.Add("request-id", "req_reset");
            return response;
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failure.StatusCode.ShouldBe(500);
        failure.RequestId.ShouldBe(new ProviderRequestId("req_reset"));
        failure.ProviderCode.ShouldBeNull();
        failure.SafeMessage.ShouldBe("The Anthropic request failed with HTTP status 500.");
        _ = failure.DiagnosticCause.ShouldBeOfType<IOException>();
        observer.Events.OfType<ModelResponseFailed>().Count().ShouldBe(1);
    }

    /// <summary>Verifies the transport's own timeout while reading an error body is a typed timeout that keeps the HTTP status.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTransportTimesOutDuringErrorBodyRead_ReturnsTimeoutWithStatus()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StreamContent(new FaultingReadStream(static () => new TaskCanceledException("The transport timed out.", new TimeoutException())))
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failure.StatusCode.ShouldBe(502);
        _ = failure.DiagnosticCause.ShouldBeOfType<TaskCanceledException>();
        observer.Events.OfType<ModelResponseFailed>().Count().ShouldBe(1);
    }

    /// <summary>Verifies caller cancellation while a request is still in flight (before any response) returns a typed cancellation.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelsWhileSendIsInFlight_ReturnsCancelledResult()
    {
        var handler = new GatedSendHttpMessageHandler();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")));
        var observer = new RecordingModelResponseObserver();
        using var cancellation = new CancellationTokenSource();

        var pending = model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, cancellation.Token);
        (await Task.WhenAny(handler.Entered, pending)).ShouldBe(handler.Entered);
        await cancellation.CancelAsync();
        var result = await pending;

        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
    }

    /// <summary>Verifies the injected deadline cancels a request still in flight (before any response) without wall-clock waiting.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenDeadlineExpiresWhileSendIsInFlight_ReturnsTimeoutFailure()
    {
        var clock = new FakeTimeProvider(Now);
        var handler = new GatedSendHttpMessageHandler();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), timeProvider: clock);
        var observer = new RecordingModelResponseObserver();

        var pending = model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddSeconds(1)), observer, TestContext.Current.CancellationToken);
        (await Task.WhenAny(handler.Entered, pending)).ShouldBe(handler.Entered);
        clock.Advance(TimeSpan.FromSeconds(2));
        var result = await pending;

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
    }

    /// <summary>Verifies the injected deadline cancels a blocked success-body read without wall-clock waiting.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenDeadlineExpiresDuringSuccessBodyRead_ReturnsTimeoutFailure()
    {
        var clock = new FakeTimeProvider(Now);
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(body),
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), timeProvider: clock);
        var observer = new RecordingModelResponseObserver();

        var pending = model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddSeconds(1)), observer, TestContext.Current.CancellationToken);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        clock.Advance(TimeSpan.FromSeconds(2));
        var result = await pending;

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        observer.Events.OfType<ModelResponseFailed>().Count().ShouldBe(1);
    }

    /// <summary>Verifies the transport's own timeout while reading a success body is a typed timeout, never an escaping exception.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTransportTimesOutDuringSuccessBodyRead_ReturnsTypedTimeoutFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new FaultingReadStream(static () => new TaskCanceledException("The transport timed out.", new TimeoutException()))),
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<TaskCanceledException>();
        observer.Events.OfType<ModelResponseFailed>().Count().ShouldBe(1);
    }

    /// <summary>Verifies observer cancellation during failure publication cannot trigger a second terminal event.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenFailureObserverThrowsCancellation_DoesNotPublishSecondTerminalEvent()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")));
        using var cancellation = new CancellationTokenSource();
        var observer = new CancellingFailureObserver(cancellation);
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, cancellation.Token));
        observer.TerminalEventCount.ShouldBe(1);
        cancellation.IsCancellationRequested.ShouldBeTrue();
    }

    /// <summary>Verifies a missing error body still produces a bounded typed failure.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenErrorBodyIsAbsent_ReturnsTypedFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict));
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")));
        var failure = (await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), new RecordingModelResponseObserver(), TestContext.Current.CancellationToken)).ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failure.SafeMessage.ShouldBe("The Anthropic request failed with HTTP status 409.");
    }

    /// <summary>Verifies an unknown body code retains diagnostics while HTTP supplies classification and body text stays unsafe.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenBodyCodeIsUnknown_UsesHttpFallbackWithoutLeakingMessage()
    {
        const string secret = "credential=do-not-leak";
        var handler = new StubHttpMessageHandler(_request =>
        {
            var body = JsonSerializer.Serialize(new { error = new { type = "future_error", message = secret } });
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent(body)
            };
            _ = response.Headers.TryAddWithoutValidation("request-id", "req_test");
            return response;
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: new AnthropicProviderOptions { BaseAddress = new Uri("https://api.anthropic.test/"), PreferStreaming = false });
        var failure = (await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), new RecordingModelResponseObserver(), TestContext.Current.CancellationToken)).ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failure.ProviderCode.ShouldBe("future_error");
        failure.RequestId.ShouldBe(new ProviderRequestId("req_test"));
        failure.SafeMessage.ShouldNotContain(secret);
    }

    /// <summary>Verifies unsupported encrypted reasoning is rejected by translation before HTTP egress.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenReasoningUsesEncryptedSignature_FailsBeforeHttpCall()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var reasoning = new ReasoningPart(new ReasoningContent(null, ReasoningVisibility.EncryptedSignature, "opaque", ExtensionData.Empty), ExtensionData.Empty);
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), TestModels.ClaudeSonnet, [TestMessages.Assistant(reasoning)], [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        var request = new LlmModelRequest(context, 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")));
        var failure = (await model.ExecuteAsync(request, new RecordingModelResponseObserver(), TestContext.Current.CancellationToken)).ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenOAuthTokenExpired_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new AnthropicProviderOptions
        {
            BaseAddress = new Uri("https://api.anthropic.test/"),
            PreferStreaming = false
        };
        var expiredCredential = new OAuthTokenProviderCredential("expired-token", Now.AddMinutes(-5));
        var model = CreateModel(handler, new StaticProviderCredentialSource(expiredCredential), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
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
        var options = new AnthropicProviderOptions
        {
            BaseAddress = new Uri("https://api.anthropic.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), descriptor: TestModels.NoToolSupport, options: options);
        var tools = ImmutableArray.Create(new LlmToolDefinition(new ToolId("get_weather"), "get_weather", null, JsonDocument.Parse("{}").RootElement));
        var request = CreateRequest(TestModels.NoToolSupport, Now.AddMinutes(1), tools);
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestModelIdentityDiffersFromAdapter_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: new AnthropicProviderOptions { BaseAddress = new Uri("https://api.anthropic.test/"), PreferStreaming = false });
        var requestDescriptor = TestModels.ClaudeSonnet with { ModelId = new ModelId("different-model") };
        var request = CreateRequest(requestDescriptor, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failed.Failure.SafeMessage.ShouldBe("The request model descriptor does not match the configured adapter descriptor.");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestCapabilitiesDifferFromAdapter_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: new AnthropicProviderOptions { BaseAddress = new Uri("https://api.anthropic.test/"), PreferStreaming = false });
        var requestDescriptor = TestModels.ClaudeSonnet with
        {
            Capabilities = TestModels.ClaudeSonnet.Capabilities with { SupportsStructuredOutput = !TestModels.ClaudeSonnet.Capabilities.SupportsStructuredOutput },
        };
        var request = CreateRequest(requestDescriptor, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ModelAttemptFailed>().Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenParallelToolCallsAreUnsupported_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var descriptor = TestModels.ClaudeSonnet with
        {
            Capabilities = TestModels.ClaudeSonnet.Capabilities with { SupportsParallelToolCalls = false },
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), descriptor: descriptor, options: new AnthropicProviderOptions { BaseAddress = new Uri("https://api.anthropic.test/"), PreferStreaming = false });
        var tools = ImmutableArray.Create(new LlmToolDefinition(new ToolId("get_weather"), "get_weather", null, JsonDocument.Parse("{}").RootElement));
        var settings = LlmRequestSettings.Default with { ParallelToolCalls = true };
        var request = CreateRequest(descriptor, Now.AddMinutes(1), tools, settings: settings);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failed.Failure.SafeMessage.ShouldBe("The selected model does not support parallel tool calls.");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeadlineAlreadyElapsed_FailsWithTimeoutWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new AnthropicProviderOptions
        {
            BaseAddress = new Uri("https://api.anthropic.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddSeconds(-1));
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
        var options = new AnthropicProviderOptions
        {
            BaseAddress = new Uri("https://api.anthropic.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
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
        var options = new AnthropicProviderOptions
        {
            BaseAddress = new Uri("https://api.anthropic.test/"),
            PreferStreaming = true
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
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
        var options = new AnthropicProviderOptions
        {
            BaseAddress = new Uri("https://api.anthropic.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: options);
        var userIdValue = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("end-user-42")]);
        var providerOptions = new ProviderRequestOptions(new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("metadata", userIdValue)));
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1), options: providerOptions);
        var observer = new RecordingModelResponseObserver();
        _ = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["metadata"]!.GetValue<string>().ShouldBe("end-user-42");
    }

    /// <summary>Verifies the transport's own timeout is a typed timeout failure, never an escaping exception or a caller cancellation.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenHttpClientTimeoutFiresWithoutCallerCancellation_ReturnsTypedTimeoutFailure()
    {
        // HttpClient.Timeout surfaces as TaskCanceledException while neither the caller token nor the deadline is cancelled.
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.",
            new TimeoutException("The operation was canceled.")));
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<TaskCanceledException>();
        observer.Events.OfType<ModelResponseFailed>().Count().ShouldBe(1);
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    /// <summary>Verifies a refused connection is a typed unavailable failure with one terminal event.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenConnectionIsRefused_ReturnsTypedUnavailableFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Connection refused", new System.Net.Sockets.SocketException(61)));
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<HttpRequestException>();
        observer.Events.OfType<ModelResponseFailed>().Count().ShouldBe(1);
    }

    /// <summary>Verifies a connection reset while the body streams is a typed unavailable failure with exactly one terminal event.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenBodyStreamFailsMidRead_ReturnsTypedUnavailableFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(FaultingReadStream.ConnectionReset("event: message_start\ndata: {"u8.ToArray())),
        });
        var options = new AnthropicProviderOptions { BaseAddress = new Uri("https://api.anthropic.test/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: options);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<IOException>();
        observer.Events.OfType<ModelResponseFailed>().Count().ShouldBe(1);
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    /// <summary>Verifies the fixture reports a precise argument failure instead of silently returning the full untruncated payload.</summary>
    [Fact]
    public void ResponseBodyFixture_WhenMarkerIsAbsentFromEveryLine_ThrowsInsteadOfReturningTheFullPayload()
    {
        var payload = TestResources.ReadAllBytes("responses/streaming_text.sse");

        var exception = Should.Throw<ArgumentException>(() => ResponseBodyFixture.TruncateAfterLineContaining(payload, "no-such-marker"));

        exception.ParamName.ShouldBe("marker");
    }

    /// <summary>Verifies a stream that ends after a completed text block settles with that text and the interim usage rather than an empty terminal.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenStreamFailsAfterPartialText_RetainsPartialPartsAndUsage()
    {
        var truncated = ResponseBodyFixture.TruncateAfterLineContaining(TestResources.ReadAllBytes("responses/streaming_text.sse"), "\"content_block_stop\"");
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(truncated)) });
        var options = new AnthropicProviderOptions { BaseAddress = new Uri("https://api.anthropic.test/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: options);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");
        var usage = failed.Usage.ShouldNotBeNull();
        usage.ReportState.ShouldBe(ModelUsageReportState.Interim);
        usage.InputTokens.ShouldBe(10);
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
        terminal.PartialParts.ShouldBe(failed.PartialParts);
        terminal.Usage.ShouldBe(failed.Usage);
    }

    /// <summary>Verifies a transport fault after a completed block reports the parts the observer already saw completed.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenBodyStreamFaultsAfterCompletedPart_RetainsCompletedPartsInFailure()
    {
        var prefix = ResponseBodyFixture.TruncateAfterLineContaining(TestResources.ReadAllBytes("responses/streaming_text.sse"), "\"content_block_stop\"");
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(FaultingReadStream.ConnectionReset(prefix)) });
        var options = new AnthropicProviderOptions { BaseAddress = new Uri("https://api.anthropic.test/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: options);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
        terminal.PartialParts.ShouldBe(failed.PartialParts);
        observer.Events.Select(static e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(static i => (long) i));
    }

    /// <summary>Verifies the terminal cancellation event is delivered even to an observer that rejects the caller's cancelled token.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenCancelledMidStream_DeliversTerminalEventWithCancellationTokenNone()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/streaming_text.sse", "text/event-stream");
        var options = new AnthropicProviderOptions { BaseAddress = new Uri("https://api.anthropic.test/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("sk-ant-test")), options: options);
        using var cts = new CancellationTokenSource();
        // Started, PartStarted, two deltas, PartCompleted: cancel once the text block has been completed.
        var observer = new TokenHonouringModelResponseObserver(cts, cancelAfterEventCount: 5);

        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, cts.Token);

        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseCancelled>();
        cancelled.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");
        terminal.PartialParts.ShouldBe(cancelled.PartialParts);
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        observer.Events.Select(static e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(static i => (long) i));
    }
}
