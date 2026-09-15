// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using AgentKit.Providers.MistralAI.Tests.Fakes;
using AgentKit.TestSupport;

/// <summary>Verifies MistralAILlmModel behavior and contracts.</summary>
public sealed class MistralAILlmModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static LlmModelRequest CreateRequest(ModelDescriptor descriptor, DateTimeOffset deadline, ImmutableArray<LlmToolDefinition> tools = default, ProviderRequestOptions? options = null)
    {
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), descriptor, [TestMessages.User("Hello!")], tools.IsDefault ? [] : tools, LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        return new LlmModelRequest(context, attempt: 1, deadline, options ?? ProviderRequestOptions.Empty);
    }

    private static MistralAILlmModel CreateModel(HttpMessageHandler handler, IProviderCredentialSource credentials, ModelDescriptor? descriptor = null, TimeProvider? timeProvider = null, MistralAIProviderOptions? options = null) => new(descriptor ?? TestModels.MistralLarge, options ?? new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/") }, new MistralAIRequestTranslator(), new MistralAIResponseParser(new SequentialToolCallIdGenerator()), credentials, new HttpClient(handler), timeProvider ?? new FakeTimeProvider(Now));
    [Fact]
    public async Task ExecuteAsync_WhenNonStreamingSuccess_SendsBearerHeaderAndChatCompletionsUri()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://api.mistral.test/v1/"),
            PreferStreaming = false,
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        var request = CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello! How can I help you today?");
        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.Method.ShouldBe(HttpMethod.Post);
        sentRequest.RequestUri.ShouldBe(new Uri("https://api.mistral.test/v1/chat/completions"));
        sentRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        sentRequest.Headers.Authorization!.Parameter.ShouldBe("mistral-test-key");
        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["stream"]!.GetValue<bool>().ShouldBeFalse();
        sentBody["model"]!.GetValue<string>().ShouldBe("mistral-large-latest");
    }

    [Fact]
    public async Task ExecuteAsync_WhenStreamingSuccess_ReturnsCompletedResponse()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/streaming_text.sse", "text/event-stream");
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://api.mistral.test/v1/"),
            PreferStreaming = true,
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        var request = CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");
        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["stream"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnauthenticated_ReturnsAuthenticationFailureWithStatusCodeAndSafeMessage()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.Unauthorized, "responses/error_401.json");
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://api.mistral.test/v1/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("bad-key")), options: options);
        var request = CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failed.Failure.StatusCode.ShouldBe(401);
        failed.Failure.SafeMessage.ShouldBe("Unauthorized");
    }

    [Fact]
    public async Task ExecuteAsync_WhenThrottled_ReturnsThrottlingFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.TooManyRequests, "responses/error_429.json");
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://api.mistral.test/v1/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        var request = CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1));
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
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://api.mistral.test/v1/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        var request = CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
        failed.Failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(45));
    }

    /// <summary>Verifies Mistral relies entirely on the shared HTTP status table, including the normalized 504 timeout.</summary>
    [Theory]
    [InlineData(408, ProviderFailureKind.Timeout)]
    [InlineData(504, ProviderFailureKind.Timeout)]
    [InlineData(503, ProviderFailureKind.Unavailable)]
    public async Task ExecuteAsync_WhenErrorStatusHasNoUsableBody_UsesSharedHttpStatusTable(int statusCode, ProviderFailureKind expected)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage((HttpStatusCode) statusCode) { Content = new StringContent("not-json", Encoding.UTF8, "text/plain") });
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://api.mistral.test/v1/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        var request = CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1));

        var result = await model.ExecuteAsync(request, new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(expected);
        failed.Failure.StatusCode.ShouldBe(statusCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationErrorArray_JoinsFieldMessagesIntoSafeMessage()
    {
        var handler = StubHttpMessageHandler.FromFixture((HttpStatusCode) 422, "responses/error_422_validation.json");
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://api.mistral.test/v1/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        var request = CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failed.Failure.SafeMessage.ShouldBe("Input should be 'system', 'user', 'assistant' or 'tool' Input should be less than or equal to 1.5");
    }

    [Fact]
    public async Task ExecuteAsync_WhenInternalServerError_ReturnsUnavailableFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.InternalServerError, "responses/error_500.json");
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://api.mistral.test/v1/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        var request = CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1));
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
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://api.mistral.test/v1/"),
            PreferStreaming = false
        };
        var expiredCredential = new OAuthTokenProviderCredential("expired-token", Now.AddMinutes(-5));
        var model = CreateModel(handler, new StaticProviderCredentialSource(expiredCredential), options: options);
        var request = CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1));
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
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://api.mistral.test/v1/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), descriptor: TestModels.NoToolSupport, options: options);
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
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://api.mistral.test/v1/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        var request = CreateRequest(TestModels.MistralLarge, Now.AddSeconds(-1));
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
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://api.mistral.test/v1/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        var request = CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1));
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
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://api.mistral.test/v1/"),
            PreferStreaming = true
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        var request = CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1));
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
        var options = new MistralAIProviderOptions
        {
            BaseAddress = new Uri("https://api.mistral.test/v1/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        var cacheKeyValue = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("end-user-42")]);
        var providerOptions = new ProviderRequestOptions(new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("prompt_cache_key", cacheKeyValue)));
        var request = CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1), options: providerOptions);
        var observer = new RecordingModelResponseObserver();
        _ = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["prompt_cache_key"]!.GetValue<string>().ShouldBe("end-user-42");
    }

    /// <summary>Verifies the transport's own timeout is a typed timeout failure, never an escaping exception or a caller cancellation.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenHttpClientTimeoutFiresWithoutCallerCancellation_ReturnsTypedTimeoutFailure()
    {
        // HttpClient.Timeout surfaces as TaskCanceledException while neither the caller token nor the deadline is cancelled.
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.",
            new TimeoutException("The operation was canceled.")));
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

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
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<HttpRequestException>();
        observer.Events.OfType<ModelResponseFailed>().Count().ShouldBe(1);
    }

    /// <summary>Verifies caller cancellation while reading an error body returns one cancellation outcome that keeps the HTTP evidence.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelsDuringErrorBodyRead_ReturnsCancelledResult()
    {
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StreamContent(body)
            };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(17));
            return response;
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        var observer = new RecordingModelResponseObserver();
        using var cancellation = new CancellationTokenSource();

        var pending = model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1)), observer, cancellation.Token);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        await cancellation.CancelAsync();
        var result = await pending;

        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>().Cancellation;
        cancelled.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        cancelled.StatusCode.ShouldBe(429);
        cancelled.RetryAfter.ShouldBe(TimeSpan.FromSeconds(17));
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseCancelled>();
    }

    /// <summary>Verifies a connection fault while reading an error body still yields the status-mapped failure with the fault retained as diagnostics.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenErrorBodyReadFails_ReturnsStatusOnlyFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StreamContent(FaultingReadStream.ConnectionReset("{\"detail\":"u8.ToArray()))
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failure.StatusCode.ShouldBe(500);
        failure.SafeMessage.ShouldBe("The provider returned HTTP status 500.");
        _ = failure.DiagnosticCause.ShouldBeOfType<IOException>();
        observer.Events.OfType<ModelResponseFailed>().Count().ShouldBe(1);
    }

    /// <summary>Verifies a connection reset while the body streams is a typed unavailable failure with exactly one terminal event.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenBodyStreamFailsMidRead_ReturnsTypedUnavailableFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(FaultingReadStream.ConnectionReset("data: {"u8.ToArray())),
        });
        var options = new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<IOException>();
        observer.Events.OfType<ModelResponseFailed>().Count().ShouldBe(1);
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }
}
