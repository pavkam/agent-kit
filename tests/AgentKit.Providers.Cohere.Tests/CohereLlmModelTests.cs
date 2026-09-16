// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Tests;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using AgentKit.Providers.Cohere.Tests.Fakes;
using AgentKit.Providers.Http;
using AgentKit.TestSupport;

/// <summary>Verifies CohereLlmModel behavior and contracts.</summary>
public sealed class CohereLlmModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static LlmModelRequest CreateRequest(ModelDescriptor descriptor, DateTimeOffset deadline, ImmutableArray<LlmToolDefinition> tools = default, ProviderRequestOptions? options = null, LlmRequestSettings? settings = null)
    {
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), descriptor, [TestMessages.User("Hello!")], tools.IsDefault ? [] : tools, LlmToolChoice.Auto, settings ?? LlmRequestSettings.Default, ExtensionData.Empty);
        return new LlmModelRequest(context, attempt: 1, deadline, options ?? ProviderRequestOptions.Empty);
    }

    private static CohereLlmModel CreateModel(HttpMessageHandler handler, IProviderCredentialSource credentials, ModelDescriptor? descriptor = null, TimeProvider? timeProvider = null, CohereProviderOptions? options = null) => new(descriptor ?? TestModels.CommandAPlus, options ?? new CohereProviderOptions { BaseAddress = new Uri("https://api.cohere.test/") }, new CohereRequestTranslator(), new CohereResponseParser(new SequentialToolCallIdGenerator()), credentials, new HttpClient(handler), timeProvider ?? new FakeTimeProvider(Now));
    [Fact]
    public async Task ExecuteAsync_WhenNonStreamingSuccess_SendsBearerHeaderAndChatUri()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://api.cohere.test/"),
            PreferStreaming = false,
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var request = CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello! How can I help you today?");
        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.Method.ShouldBe(HttpMethod.Post);
        sentRequest.RequestUri.ShouldBe(new Uri("https://api.cohere.test/v2/chat"));
        sentRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        sentRequest.Headers.Authorization!.Parameter.ShouldBe("cohere-test-key");
        sentRequest.Headers.Contains("X-Client-Name").ShouldBeFalse();
        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["stream"]!.GetValue<bool>().ShouldBeFalse();
        sentBody["model"]!.GetValue<string>().ShouldBe("command-a-plus-05-2026");
    }

    [Fact]
    public async Task ExecuteAsync_WhenClientNameConfigured_SendsXClientNameHeader()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://api.cohere.test/"),
            PreferStreaming = false,
            ClientName = "agentkit",
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var request = CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        _ = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        handler.Requests[0].Headers.GetValues("X-Client-Name").ShouldContain("agentkit");
    }

    [Fact]
    public async Task ExecuteAsync_WhenStreamingSuccess_ReturnsCompletedResponse()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/streaming_text.sse", "text/event-stream");
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://api.cohere.test/"),
            PreferStreaming = true,
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var request = CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1));
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
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://api.cohere.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("bad-key")), options: options);
        var request = CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failed.Failure.StatusCode.ShouldBe(401);
        failed.Failure.SafeMessage.ShouldBe("The provider returned HTTP status 401.");
        ProviderErrorMessageEvidence.TryRead(failed.Failure.Extensions).ShouldBe("invalid api token");
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvalidTokenStatus498_ReturnsAuthenticationFailureNotThrottling()
    {
        var handler = StubHttpMessageHandler.FromFixture((HttpStatusCode) 498, "responses/error_498.json");
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://api.cohere.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var request = CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failed.Failure.StatusCode.ShouldBe(498);
    }

    [Fact]
    public async Task ExecuteAsync_WhenThrottled_ReturnsThrottlingFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.TooManyRequests, "responses/error_429.json");
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://api.cohere.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var request = CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1));
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
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://api.cohere.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var request = CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
        failed.Failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(45));
    }

    /// <summary>Verifies Cohere-specific status overrides sit on top of the shared HTTP status table; a server-sent 499 is never caller cancellation.</summary>
    [Theory]
    [InlineData(402, ProviderFailureKind.Authorization)]
    [InlineData(408, ProviderFailureKind.Timeout)]
    [InlineData(498, ProviderFailureKind.Authentication)]
    [InlineData(499, ProviderFailureKind.InvalidRequest)]
    [InlineData(501, ProviderFailureKind.InvalidRequest)]
    [InlineData(504, ProviderFailureKind.Timeout)]
    public async Task ExecuteAsync_WhenErrorStatusHasNoUsableBody_UsesSharedTableWithCohereOverrides(int statusCode, ProviderFailureKind expected)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage((HttpStatusCode) statusCode) { Content = new StringContent("not-json", Encoding.UTF8, "text/plain") });
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://api.cohere.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var request = CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1));

        var result = await model.ExecuteAsync(request, new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(expected);
        failed.Failure.StatusCode.ShouldBe(statusCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInternalServerError_ReturnsUnavailableFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.InternalServerError, "responses/error_500.json");
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://api.cohere.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var request = CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1));
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
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://api.cohere.test/"),
            PreferStreaming = false
        };
        var expiredCredential = new OAuthTokenProviderCredential("expired-token", Now.AddMinutes(-5));
        var model = CreateModel(handler, new StaticProviderCredentialSource(expiredCredential), options: options);
        var request = CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1));
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
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://api.cohere.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), descriptor: TestModels.NoToolSupport, options: options);
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
        const string hostileBody = /*lang=json,strict*/ """{ "message": "Authorization failed for sk-live-super-secret; internal tenant alice@example.test." }""";
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(hostileBody, Encoding.UTF8, "application/json"),
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: new CohereProviderOptions { BaseAddress = new Uri("https://api.cohere.test/"), PreferStreaming = false });
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

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
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: new CohereProviderOptions { BaseAddress = new Uri("https://api.cohere.test/"), PreferStreaming = false });
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

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
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: new CohereProviderOptions { BaseAddress = new Uri("https://api.cohere.test/"), PreferStreaming = false });
        var requestDescriptor = TestModels.CommandAPlus with { ModelId = new ModelId("different-model") };
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
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: new CohereProviderOptions { BaseAddress = new Uri("https://api.cohere.test/"), PreferStreaming = false });
        var requestDescriptor = TestModels.CommandAPlus with
        {
            Capabilities = TestModels.CommandAPlus.Capabilities with { SupportsStructuredOutput = !TestModels.CommandAPlus.Capabilities.SupportsStructuredOutput },
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
        var descriptor = TestModels.CommandAPlus with
        {
            Capabilities = TestModels.CommandAPlus.Capabilities with { SupportsParallelToolCalls = false },
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), descriptor: descriptor, options: new CohereProviderOptions { BaseAddress = new Uri("https://api.cohere.test/"), PreferStreaming = false });
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
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://api.cohere.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var request = CreateRequest(TestModels.CommandAPlus, Now.AddSeconds(-1));
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
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://api.cohere.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var request = CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1));
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
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://api.cohere.test/"),
            PreferStreaming = true
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var request = CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1));
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
        var options = new CohereProviderOptions
        {
            BaseAddress = new Uri("https://api.cohere.test/"),
            PreferStreaming = false
        };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var safetyModeValue = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("STRICT")]);
        var providerOptions = new ProviderRequestOptions(new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("safety_mode", safetyModeValue)));
        var request = CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1), options: providerOptions);
        var observer = new RecordingModelResponseObserver();
        _ = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["safety_mode"]!.GetValue<string>().ShouldBe("STRICT");
    }

    /// <summary>Verifies the transport's own timeout is a typed timeout failure, never an escaping exception or a caller cancellation.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenHttpClientTimeoutFiresWithoutCallerCancellation_ReturnsTypedTimeoutFailure()
    {
        // HttpClient.Timeout surfaces as TaskCanceledException while neither the caller token nor the deadline is cancelled.
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.",
            new TimeoutException("The operation was canceled.")));
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

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
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

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
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")));
        var observer = new RecordingModelResponseObserver();
        using var cancellation = new CancellationTokenSource();

        var pending = model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1)), observer, cancellation.Token);
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
            Content = new StreamContent(FaultingReadStream.ConnectionReset("{\"message\":"u8.ToArray()))
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

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
        var options = new CohereProviderOptions { BaseAddress = new Uri("https://api.cohere.test/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<IOException>();
        observer.Events.OfType<ModelResponseFailed>().Count().ShouldBe(1);
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    /// <summary>Verifies a stream that ends after a completed text block settles with that text rather than an empty terminal.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenStreamFailsAfterPartialText_RetainsPartialPartsAndUsage()
    {
        var truncated = ResponseBodyFixture.TruncateAfterLineContaining(TestResources.ReadAllBytes("responses/streaming_text.sse"), "\"content-end\"");
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(truncated)) });
        var options = new CohereProviderOptions { BaseAddress = new Uri("https://api.cohere.test/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");
        // Cohere reports usage only on message-end, so an interrupted stream truthfully has none.
        failed.Usage.ShouldBeNull();
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
        terminal.PartialParts.ShouldBe(failed.PartialParts);
        terminal.Usage.ShouldBeNull();
    }

    /// <summary>Verifies a transport fault after a completed block reports the parts the observer already saw completed.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenBodyStreamFaultsAfterCompletedPart_RetainsCompletedPartsInFailure()
    {
        var prefix = ResponseBodyFixture.TruncateAfterLineContaining(TestResources.ReadAllBytes("responses/streaming_text.sse"), "\"content-end\"");
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(FaultingReadStream.ConnectionReset(prefix)) });
        var options = new CohereProviderOptions { BaseAddress = new Uri("https://api.cohere.test/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

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
        var options = new CohereProviderOptions { BaseAddress = new Uri("https://api.cohere.test/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        using var cts = new CancellationTokenSource();
        // Started, PartStarted, two deltas, PartCompleted: cancel once the text block has been completed.
        var observer = new TokenHonouringModelResponseObserver(cts, cancelAfterEventCount: 5);

        var result = await model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1)), observer, cts.Token);

        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseCancelled>();
        cancelled.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");
        terminal.PartialParts.ShouldBe(cancelled.PartialParts);
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        observer.Events.Select(static e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(static i => (long) i));
    }

    /// <summary>Verifies a translator that cannot honor the request's settings fails with a typed invalid-request outcome without ever sending the HTTP request.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTranslationThrowsNotSupportedException_FailsWithInvalidRequestWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: new CohereProviderOptions { BaseAddress = new Uri("https://api.cohere.test/"), PreferStreaming = false });
        var settings = LlmRequestSettings.Default with { ParallelToolCalls = false };
        var request = CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1), settings: settings);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failed.Failure.SafeMessage.ShouldBe("The request could not be translated for the Cohere v2 Chat wire format.");
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<NotSupportedException>();
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies caller cancellation while the request is still being sent (before any response headers arrive) returns one cancelled outcome.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelsWhileSendIsInFlight_ReturnsCancelledResult()
    {
        var handler = new GatedSendHttpMessageHandler();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")));
        var observer = new RecordingModelResponseObserver();
        using var cancellation = new CancellationTokenSource();

        var pending = model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1)), observer, cancellation.Token);
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
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), timeProvider: timeProvider);
        var observer = new RecordingModelResponseObserver();

        var pending = model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddSeconds(5)), observer, TestContext.Current.CancellationToken);
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
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), timeProvider: timeProvider);
        var observer = new RecordingModelResponseObserver();

        var pending = model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddSeconds(5)), observer, TestContext.Current.CancellationToken);
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
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failed.Failure.StatusCode.ShouldBe(429);
        failed.Failure.SafeMessage.ShouldBe("The transport timed out while the provider error response was being received.");
    }

    /// <summary>Verifies the request deadline elapsing while the successful response body is still streaming in returns a typed timeout with truthful partial output.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenDeadlineElapsesDuringSuccessfulBodyRead_ReturnsTypedTimeoutFailure()
    {
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });
        var timeProvider = new FakeTimeProvider(Now);
        var options = new CohereProviderOptions { BaseAddress = new Uri("https://api.cohere.test/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), timeProvider: timeProvider, options: options);
        var observer = new RecordingModelResponseObserver();

        var pending = model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddSeconds(5)), observer, TestContext.Current.CancellationToken);
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
        var options = new CohereProviderOptions { BaseAddress = new Uri("https://api.cohere.test/"), PreferStreaming = true };
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")), options: options);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.CommandAPlus, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failed.Failure.SafeMessage.ShouldBe("The transport timed out while the response body was being received.");
    }
}
