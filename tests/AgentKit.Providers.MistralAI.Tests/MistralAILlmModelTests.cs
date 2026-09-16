// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using AgentKit.Providers.Http;
using AgentKit.Providers.MistralAI.Tests.Fakes;
using AgentKit.TestSupport;

/// <summary>Verifies MistralAILlmModel behavior and contracts.</summary>
public sealed class MistralAILlmModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static LlmModelRequest CreateRequest(ModelDescriptor descriptor, DateTimeOffset deadline, ImmutableArray<LlmToolDefinition> tools = default, ProviderRequestOptions? options = null, LlmRequestSettings? settings = null)
    {
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), descriptor, [TestMessages.User("Hello!")], tools.IsDefault ? [] : tools, LlmToolChoice.Auto, settings ?? LlmRequestSettings.Default, ExtensionData.Empty);
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
        failed.Failure.SafeMessage.ShouldBe("The provider returned HTTP status 401.");
        ProviderErrorMessageEvidence.TryRead(failed.Failure.Extensions).ShouldBe("Unauthorized");
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
    public async Task ExecuteAsync_WhenValidationErrorArray_JoinsFieldMessagesIntoDiagnosticEvidenceOnly()
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
        failed.Failure.SafeMessage.ShouldBe("The provider returned HTTP status 422.");
        ProviderErrorMessageEvidence.TryRead(failed.Failure.Extensions).ShouldBe("Input should be 'system', 'user', 'assistant' or 'tool' Input should be less than or equal to 1.5");
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
    public async Task ExecuteAsync_WhenErrorBodyContainsHostileText_DoesNotExposeItAsSafeMessage()
    {
        const string hostileBody = /*lang=json,strict*/ """{ "detail": "Authorization failed for sk-live-super-secret; internal tenant alice@example.test." }""";
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(hostileBody, Encoding.UTF8, "application/json"),
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/"), PreferStreaming = false });
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failure.SafeMessage.ShouldBe("The provider returned HTTP status 401.");
        failure.SafeMessage.ShouldNotContain("sk-live-super-secret");
        failure.SafeMessage.ShouldNotContain("alice@example.test");
        failure.ProviderCode.ShouldBeNull();
        failure.DiagnosticCause.ShouldBeNull();
        ProviderErrorMessageEvidence.TryRead(failure.Extensions).ShouldBe("Authorization failed for sk-live-super-secret; internal tenant alice@example.test.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenErrorBodyIsNotJson_FallsBackToGenericSafeMessage()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>Bad Gateway</html>", Encoding.UTF8, "text/html"),
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/"), PreferStreaming = false });
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failure.StatusCode.ShouldBe(502);
        failure.SafeMessage.ShouldBe("The provider returned HTTP status 502.");
        _ = failure.DiagnosticCause.ShouldBeOfType<JsonException>();
        ProviderErrorMessageEvidence.TryRead(failure.Extensions).ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestModelIdentityDiffersFromAdapter_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/"), PreferStreaming = false });
        var requestDescriptor = TestModels.MistralLarge with { ModelId = new ModelId("different-model") };
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
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/"), PreferStreaming = false });
        var requestDescriptor = TestModels.MistralLarge with
        {
            Capabilities = TestModels.MistralLarge.Capabilities with { SupportsStructuredOutput = !TestModels.MistralLarge.Capabilities.SupportsStructuredOutput },
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
        var descriptor = TestModels.MistralLarge with
        {
            Capabilities = TestModels.MistralLarge.Capabilities with { SupportsParallelToolCalls = false },
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), descriptor: descriptor, options: new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/"), PreferStreaming = false });
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

    /// <summary>Verifies a stream that ends after streamed text settles with that text and the interim usage rather than an empty terminal.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenStreamFailsAfterPartialText_RetainsPartialPartsAndUsage()
    {
        const string truncatedBody = """
            data: {"id": "cmpl-s01", "model": "mistral-large-latest-2412", "choices": [{"index": 0, "delta": {"role": "assistant", "content": "Hello"}, "finish_reason": null}], "usage": {"prompt_tokens": 10, "completion_tokens": 1, "total_tokens": 11}}


            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(truncatedBody, Encoding.UTF8, "text/event-stream") });
        var options = new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello");
        var usage = failed.Usage.ShouldNotBeNull();
        usage.ReportState.ShouldBe(ModelUsageReportState.Interim);
        usage.InputTokens.ShouldBe(10);
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
        terminal.PartialParts.ShouldBe(failed.PartialParts);
        terminal.Usage.ShouldBe(failed.Usage);
    }

    /// <summary>Verifies a transport fault after a completed part reports the parts the observer already saw completed.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenBodyStreamFaultsAfterCompletedPart_RetainsCompletedPartsInFailure()
    {
        // A non-text content chunk is opened and completed in one step, so it is a completed part before the fault.
        const string prefix = """
            data: {"id": "cmpl-s03", "model": "mistral-large-latest-2412", "choices": [{"index": 0, "delta": {"role": "assistant", "content": [{"type": "image_url", "image_url": "https://example.com/generated.png"}]}, "finish_reason": null}]}


            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(FaultingReadStream.ConnectionReset(Encoding.UTF8.GetBytes(prefix))) });
        var options = new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<UnknownContentPart>().TypeName.ShouldBe("image_url");
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
        terminal.PartialParts.ShouldBe(failed.PartialParts);
        observer.Events.Select(static e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(static i => (long) i));
    }

    /// <summary>Verifies the terminal cancellation event is delivered even to an observer that rejects the caller's cancelled token.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenCancelledMidStream_DeliversTerminalEventWithCancellationTokenNone()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/streaming_text.sse", "text/event-stream");
        var options = new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        using var cts = new CancellationTokenSource();
        // Started, PartStarted, two deltas, PartCompleted: cancel once the text part has been completed.
        var observer = new TokenHonouringModelResponseObserver(cts, cancelAfterEventCount: 5);

        var result = await model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1)), observer, cts.Token);

        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseCancelled>();
        cancelled.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");
        terminal.PartialParts.ShouldBe(cancelled.PartialParts);
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        observer.Events.Select(static e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(static i => (long) i));
    }

    /// <summary>Verifies a translator that cannot derive a distinct wire tool-call identifier fails with a typed invalid-request outcome without ever sending the HTTP request.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTranslationThrowsNotSupportedException_FailsWithInvalidRequestWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/"), PreferStreaming = false });

        var victimCallId = new ToolCallId(Guid.Parse("00000000-0000-0000-0000-000000000040"));
        var toolReference = new ToolReference(new ToolAlias("noop"), null, null);
        var blockers = Enumerable.Range(0, 16)
            .Select(disambiguator => new ToolCallPart(
                new ToolCallId(Guid.Parse($"00000000-0000-0000-0000-0000000001{disambiguator:x2}")),
                toolReference,
                JsonDocument.Parse("{}").RootElement,
                new ProviderToolCallId(MistralAIToolCallIdCodec.Encode(victimCallId, disambiguator)),
                ExtensionData.Empty))
            .ToArray<ContentPart>();
        var victim = new ToolCallPart(victimCallId, toolReference, JsonDocument.Parse("{}").RootElement, providerCallId: null, ExtensionData.Empty);

        var context = new LlmRequestContext(
            new ModelRequestId(Guid.NewGuid()),
            TestModels.MistralLarge,
            [TestMessages.Assistant(blockers), TestMessages.Assistant(victim)],
            [],
            LlmToolChoice.Auto,
            LlmRequestSettings.Default,
            ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failed.Failure.SafeMessage.ShouldBe("The request could not be translated for the Mistral AI Chat Completions wire format.");
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<NotSupportedException>();
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies an error body with no 'detail' field at all yields no provider error evidence rather than throwing.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenErrorBodyHasNoDetailField_ReturnsFailureWithNoProviderErrorEvidence()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(/*lang=json,strict*/ """{ "message": "unauthorized" }""", Encoding.UTF8, "application/json"),
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        ProviderErrorMessageEvidence.TryRead(failure.Extensions).ShouldBeNull();
    }

    /// <summary>Verifies a 'detail' field of an unexpected JSON kind (neither string nor array) yields no provider error evidence rather than throwing.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenDetailFieldIsUnexpectedJsonKind_ReturnsFailureWithNoProviderErrorEvidence()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(/*lang=json,strict*/ """{ "detail": 42 }""", Encoding.UTF8, "application/json"),
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        ProviderErrorMessageEvidence.TryRead(failure.Extensions).ShouldBeNull();
    }

    /// <summary>Verifies caller cancellation while the request is still being sent (before any response headers arrive) returns one cancelled outcome.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelsWhileSendIsInFlight_ReturnsCancelledResult()
    {
        var handler = new GatedSendHttpMessageHandler();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        var observer = new RecordingModelResponseObserver();
        using var cancellation = new CancellationTokenSource();

        var pending = model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1)), observer, cancellation.Token);
        (await Task.WhenAny(handler.Entered, pending)).ShouldBe(handler.Entered);
        await cancellation.CancelAsync();
        var result = await pending;

        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseCancelled>();
    }

    /// <summary>Verifies the request deadline elapsing while the request is still being sent is a typed timeout, not a caller cancellation.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenDeadlineElapsesWhileSendIsInFlight_ReturnsTypedTimeoutFailure()
    {
        var handler = new GatedSendHttpMessageHandler();
        var timeProvider = new FakeTimeProvider(Now);
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), timeProvider: timeProvider);
        var observer = new RecordingModelResponseObserver();

        var pending = model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddSeconds(5)), observer, TestContext.Current.CancellationToken);
        await handler.Entered;
        timeProvider.Advance(TimeSpan.FromSeconds(6));
        var result = await pending;

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failed.Failure.SafeMessage.ShouldBe("The request did not complete before its deadline.");
    }

    /// <summary>Verifies the request deadline elapsing while the provider's error response body is still being received returns a typed timeout retaining the HTTP evidence.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenDeadlineElapsesDuringErrorBodyRead_ReturnsTypedTimeoutFailureRetainingHttpEvidence()
    {
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StreamContent(body) });
        var timeProvider = new FakeTimeProvider(Now);
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), timeProvider: timeProvider);
        var observer = new RecordingModelResponseObserver();

        var pending = model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddSeconds(5)), observer, TestContext.Current.CancellationToken);
        await body.Entered;
        timeProvider.Advance(TimeSpan.FromSeconds(6));
        var result = await pending;

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failed.Failure.StatusCode.ShouldBe(429);
        failed.Failure.SafeMessage.ShouldBe("The provider error response was not received before the request deadline.");
    }

    /// <summary>Verifies a bare transport timeout (neither the caller nor the deadline) while reading the provider's error body still yields a typed timeout retaining the HTTP evidence.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTransportTimesOutDuringErrorBodyRead_ReturnsTypedTimeoutFailureRetainingHttpEvidence()
    {
        var body = new FaultingReadStream(static () => new OperationCanceledException("The transport's own read timeout elapsed."));
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StreamContent(body) });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failed.Failure.StatusCode.ShouldBe(429);
        failed.Failure.SafeMessage.ShouldBe("The transport timed out while the provider error response was being received.");
    }

    /// <summary>Verifies the request deadline elapsing while the successful response body is still streaming in returns a typed timeout.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenDeadlineElapsesDuringSuccessfulBodyRead_ReturnsTypedTimeoutFailure()
    {
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });
        var timeProvider = new FakeTimeProvider(Now);
        var options = new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), timeProvider: timeProvider, options: options);
        var observer = new RecordingModelResponseObserver();

        var pending = model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddSeconds(5)), observer, TestContext.Current.CancellationToken);
        await body.Entered;
        timeProvider.Advance(TimeSpan.FromSeconds(6));
        var result = await pending;

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failed.Failure.SafeMessage.ShouldBe("The response was not fully received before the request's deadline.");
    }

    /// <summary>Verifies a bare transport timeout (neither the caller nor the deadline) while reading the successful response body still yields a typed timeout.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTransportTimesOutDuringSuccessfulBodyRead_ReturnsTypedTimeoutFailure()
    {
        var body = new FaultingReadStream(static () => new OperationCanceledException("The transport's own read timeout elapsed."));
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });
        var options = new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), options: options);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.MistralLarge, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failed.Failure.SafeMessage.ShouldBe("The transport timed out while the response body was being received.");
    }
}
