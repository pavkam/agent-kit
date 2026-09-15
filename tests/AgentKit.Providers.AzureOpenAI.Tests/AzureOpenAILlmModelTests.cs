// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI.Tests;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

using AgentKit.Providers.AzureOpenAI.Tests.Fakes;
using AgentKit.TestSupport;

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
    private static AzureOpenAILlmModel CreateModel(StubHttpMessageHandler handler, IProviderCredentialSource credentials, ModelDescriptor descriptor, bool preferStreaming = false) => new(descriptor, AzureOpenAIProviderDefaults.CreateProfile(new AzureOpenAIProviderOptions { ResourceEndpoint = new Uri("https://my-resource.openai.azure.test/"), PreferStreaming = preferStreaming, }), new OpenAIRequestTranslator(), new OpenAIChatCompletionResponseParser(new SequentialToolCallIdGenerator()), credentials, new HttpClient(handler), new FakeTimeProvider(Now));
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
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
        failed.Failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(45));
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
    public async Task ExecuteAsync_WhenRequestModelIdentityDiffersFromAdapter_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);
        var requestDescriptor = descriptor with { ModelId = new ModelId("different-model") };
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(requestDescriptor), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failed.Failure.SafeMessage.ShouldBe("The request model descriptor does not match the configured adapter descriptor.");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestCapabilitiesDifferFromAdapter_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);
        var requestDescriptor = descriptor with
        {
            Capabilities = descriptor.Capabilities with { SupportsStructuredOutput = !descriptor.Capabilities.SupportsStructuredOutput },
        };
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(requestDescriptor), observer, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ModelAttemptFailed>().Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenParallelToolCallsAreUnsupported_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/success.json");
        var descriptor = CreateDescriptor() with
        {
            Capabilities = AzureOpenAIProviderDefaults.DefaultCapabilities with { SupportsParallelToolCalls = false },
        };
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);
        var settings = LlmRequestSettings.Default with { ParallelToolCalls = true };
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), descriptor, [], [new LlmToolDefinition(new ToolId("get_weather"), "get_weather", null, JsonDocument.Parse("{}").RootElement)], LlmToolChoice.Auto, settings, ExtensionData.Empty);
        var request = new LlmModelRequest(context, attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);
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

    /// <summary>Verifies the transport's own timeout is a typed timeout failure, never an escaping exception or a caller cancellation.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenHttpClientTimeoutFiresWithoutCallerCancellation_ReturnsTypedTimeoutFailure()
    {
        // HttpClient.Timeout surfaces as TaskCanceledException while neither the caller token nor the deadline is cancelled.
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.",
            new TimeoutException("The operation was canceled.")));
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);

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
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);

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
            response.Headers.Add("x-request-id", "req_cancelled");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(17));
            return response;
        });
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);
        var observer = new RecordingModelResponseObserver();
        using var cancellation = new CancellationTokenSource();

        var pending = model.ExecuteAsync(CreateRequest(descriptor), observer, cancellation.Token);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        await cancellation.CancelAsync();
        var result = await pending;

        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>().Cancellation;
        cancelled.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        cancelled.StatusCode.ShouldBe(429);
        cancelled.RequestId.ShouldBe(new ProviderRequestId("req_cancelled"));
        cancelled.RetryAfter.ShouldBe(TimeSpan.FromSeconds(17));
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseCancelled>();
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
            response.Headers.Add("x-request-id", "req_reset");
            return response;
        });
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failure.StatusCode.ShouldBe(500);
        failure.RequestId.ShouldBe(new ProviderRequestId("req_reset"));
        failure.ProviderCode.ShouldBeNull();
        failure.SafeMessage.ShouldBe("The provider returned HTTP status 500.");
        _ = failure.DiagnosticCause.ShouldBeOfType<IOException>();
        observer.Events.OfType<ModelResponseFailed>().Count().ShouldBe(1);
    }

    /// <summary>Verifies a connection reset while the body is being read is a typed unavailable failure with exactly one terminal event.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenBodyStreamFailsMidRead_ReturnsTypedUnavailableFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(FaultingReadStream.ConnectionReset("{\"id\":\"chatcmpl-1\","u8.ToArray())),
        });
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<IOException>();
        observer.Events.OfType<ModelResponseFailed>().Count().ShouldBe(1);
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    /// <summary>Verifies a stream that ends after streamed text settles with that text and the reported usage rather than an empty terminal.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenStreamFailsAfterPartialText_RetainsPartialPartsAndUsage()
    {
        const string truncatedBody = """
            data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1700000010,"model":"gpt-4o-2024-08-06","choices":[{"index":0,"delta":{"role":"assistant","content":"Hello"},"finish_reason":null}],"usage":{"prompt_tokens":10,"completion_tokens":1,"total_tokens":11}}


            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(truncatedBody, Encoding.UTF8, "text/event-stream") });
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor, preferStreaming: true);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello");
        failed.Usage.ShouldNotBeNull().InputTokens.ShouldBe(10);
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
        terminal.PartialParts.ShouldBe(failed.PartialParts);
        terminal.Usage.ShouldBe(failed.Usage);
        observer.Events.Select(static e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(static i => (long) i));
    }

    /// <summary>Verifies the terminal cancellation event is delivered even to an observer that rejects the caller's cancelled token.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenCancelledMidStream_DeliversTerminalEventWithCancellationTokenNone()
    {
        const string body = """
            data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1700000010,"model":"gpt-4o-2024-08-06","choices":[{"index":0,"delta":{"role":"assistant","content":"Hello"},"finish_reason":null}]}

            data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1700000010,"model":"gpt-4o-2024-08-06","choices":[{"index":0,"delta":{"content":"!"},"finish_reason":null}]}

            data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1700000010,"model":"gpt-4o-2024-08-06","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

            data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1700000010,"model":"gpt-4o-2024-08-06","choices":[],"usage":{"prompt_tokens":10,"completion_tokens":2,"total_tokens":12}}

            data: [DONE]

            """;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") });
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor, preferStreaming: true);
        using var cts = new CancellationTokenSource();
        // Started, PartStarted, two deltas, PartCompleted: cancel once the text part has been completed.
        var observer = new TokenHonouringModelResponseObserver(cts, cancelAfterEventCount: 5);

        var result = await model.ExecuteAsync(CreateRequest(descriptor), observer, cts.Token);

        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseCancelled>();
        cancelled.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello!");
        terminal.PartialParts.ShouldBe(cancelled.PartialParts);
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        observer.Events.Select(static e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(static i => (long) i));
    }
}
