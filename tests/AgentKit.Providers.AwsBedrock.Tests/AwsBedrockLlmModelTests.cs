// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

using AgentKit.Providers.AwsBedrock.Tests.Fakes;
using AgentKit.TestSupport;

/// <summary>Verifies AwsBedrockLlmModel behavior and contracts.</summary>
public sealed class AwsBedrockLlmModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static LlmModelRequest CreateRequest(ModelDescriptor descriptor, DateTimeOffset deadline, ImmutableArray<LlmToolDefinition> tools = default, ProviderRequestOptions? options = null)
    {
        var context = new LlmRequestContext(new ModelRequestId(Guid.NewGuid()), descriptor, [TestMessages.User("Hello!")], tools.IsDefault ? [] : tools, LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
        return new LlmModelRequest(context, attempt: 1, deadline, options ?? ProviderRequestOptions.Empty);
    }

    private static AwsBedrockLlmModel CreateModel(HttpMessageHandler handler, IAwsCredentialSource credentials, ModelDescriptor? descriptor = null, TimeProvider? timeProvider = null, AwsBedrockProviderOptions? options = null) => new(descriptor ?? TestModels.ClaudeSonnet, options ?? new AwsBedrockProviderOptions { Region = "us-east-1" }, new AwsBedrockRequestTranslator(), new AwsBedrockResponseParser(new SequentialToolCallIdGenerator()), credentials, new HttpClient(handler), timeProvider ?? new FakeTimeProvider(Now));
    private static StaticAwsCredentialSource CreateCredentials() => new(new AwsSigV4Credential("AKIAIOSFODNN7EXAMPLE", "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", null));
    [Fact]
    public async Task ExecuteAsync_WhenNonStreamingSuccess_SendsSignedConverseRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello! How can I help you today?");
        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.Method.ShouldBe(HttpMethod.Post);
        sentRequest.RequestUri.ShouldBe(new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/anthropic.claude-3-sonnet-20240229-v1%3A0/converse"));
        _ = sentRequest.Headers.GetValues("x-amz-date").ShouldHaveSingleItem();
        _ = sentRequest.Headers.GetValues("x-amz-content-sha256").ShouldHaveSingleItem();
        var authorization = sentRequest.Headers.GetValues("Authorization").ShouldHaveSingleItem();
        authorization.ShouldStartWith("AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/");
        authorization.ShouldContain("bedrock/aws4_request");
        sentRequest.Headers.Contains("x-amz-security-token").ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenSessionTokenCredential_IncludesSecurityTokenHeader()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var credentials = new StaticAwsCredentialSource(new AwsSigV4Credential("AKIAIOSFODNN7EXAMPLE", "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", "session-token-value"));
        var model = CreateModel(handler, credentials, options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        _ = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        handler.Requests[0].Headers.GetValues("x-amz-security-token").ShouldContain("session-token-value");
    }

    [Fact]
    public async Task ExecuteAsync_WhenStreamingSuccess_ReturnsCompletedResponseFromConverseStreamPath()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/streaming_text.bin", "application/vnd.amazon.eventstream");
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = true
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<ModelAttemptCompleted>();
        completed.Response.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe("Hello world");
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldEndWith("/converse-stream");
    }

    [Fact]
    public async Task ExecuteAsync_WhenAccessDenied_ReturnsAuthorizationFailureUsingErrorTypeHeader()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.Forbidden, "responses/error_access_denied.json", configureHeaders: response => response.Headers.Add("x-amzn-errortype", "AccessDeniedException"));
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        failed.Failure.StatusCode.ShouldBe(403);
        failed.Failure.SafeMessage.ShouldBe("User is not authorized to perform: bedrock:InvokeModel");
        failed.Failure.ProviderCode.ShouldBe("AccessDeniedException");
    }

    [Fact]
    public async Task ExecuteAsync_WhenThrottled_ReturnsThrottlingFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture((HttpStatusCode) 429, "responses/error_throttling.json", configureHeaders: response => response.Headers.Add("x-amzn-errortype", "ThrottlingException"));
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
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
            (HttpStatusCode) 429,
            "responses/error_throttling.json",
            configureHeaders: response =>
            {
                response.Headers.Add("x-amzn-errortype", "ThrottlingException");
                response.Headers.RetryAfter = new RetryConditionHeaderValue(Now.AddSeconds(45));
            });
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
        failed.Failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(45));
    }

    /// <summary>Verifies the Bedrock 424 model-error override sits on top of the shared HTTP status table when no error-type header is present.</summary>
    [Theory]
    [InlineData(408, ProviderFailureKind.Timeout)]
    [InlineData(424, ProviderFailureKind.Unavailable)]
    [InlineData(504, ProviderFailureKind.Timeout)]
    public async Task ExecuteAsync_WhenErrorStatusHasNoErrorTypeHeader_UsesSharedTableWithBedrockOverrides(int statusCode, ProviderFailureKind expected)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage((HttpStatusCode) statusCode) { Content = new StringContent("not-json", Encoding.UTF8, "text/plain") });
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));

        var result = await model.ExecuteAsync(request, new RecordingModelResponseObserver(), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(expected);
        failed.Failure.StatusCode.ShouldBe(statusCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenErrorHasNoErrorTypeHeader_FallsBackToStatusCodeMapping()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.InternalServerError, "responses/error_throttling.json");
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1));
        var observer = new RecordingModelResponseObserver();
        var result = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failed.Failure.ProviderCode.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenModelDoesNotSupportRequestedTools_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/buffered_text.json");
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), descriptor: TestModels.NoToolSupport, options: options);
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
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
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
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
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
        var payload = TestResources.ReadAllBytes("responses/streaming_text.bin");
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new DelayedStream(payload)), });
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = true
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
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
        var options = new AwsBedrockProviderOptions
        {
            Region = "us-east-1",
            PreferStreaming = false
        };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var extensionValue = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(new { latency = "optimized" })]);
        var providerOptions = new ProviderRequestOptions(new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("performanceConfig", extensionValue)));
        var request = CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1), options: providerOptions);
        var observer = new RecordingModelResponseObserver();
        _ = await model.ExecuteAsync(request, observer, TestContext.Current.CancellationToken);
        var sentBody = JsonNode.Parse(handler.RequestBodies[0].AsSpan());
        sentBody!["performanceConfig"]!["latency"]!.GetValue<string>().ShouldBe("optimized");
    }

    /// <summary>Verifies the transport's own timeout is a typed timeout failure, never an escaping exception or a caller cancellation.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenHttpClientTimeoutFiresWithoutCallerCancellation_ReturnsTypedTimeoutFailure()
    {
        // HttpClient.Timeout surfaces as TaskCanceledException while neither the caller token nor the deadline is cancelled.
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.",
            new TimeoutException("The operation was canceled.")));
        var model = CreateModel(handler, CreateCredentials());
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
        var model = CreateModel(handler, CreateCredentials());
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

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
            response.Headers.Add("x-amzn-RequestId", "req_cancelled");
            response.Headers.Add("x-amzn-errortype", "ThrottlingException");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(17));
            return response;
        });
        var model = CreateModel(handler, CreateCredentials());
        var observer = new RecordingModelResponseObserver();
        using var cancellation = new CancellationTokenSource();

        var pending = model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, cancellation.Token);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        await cancellation.CancelAsync();
        var result = await pending;

        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>().Cancellation;
        cancelled.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        cancelled.StatusCode.ShouldBe(429);
        cancelled.RequestId.ShouldBe(new ProviderRequestId("req_cancelled"));
        cancelled.ProviderCode.ShouldBe("ThrottlingException");
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
                Content = new StreamContent(FaultingReadStream.ConnectionReset("{\"message\":"u8.ToArray()))
            };
            response.Headers.Add("x-amzn-RequestId", "req_reset");
            return response;
        });
        var model = CreateModel(handler, CreateCredentials());
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<ModelAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failure.StatusCode.ShouldBe(500);
        failure.RequestId.ShouldBe(new ProviderRequestId("req_reset"));
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
            Content = new StreamContent(FaultingReadStream.ConnectionReset("{\"output\":"u8.ToArray())),
        });
        var options = new AwsBedrockProviderOptions { Region = "us-east-1", PreferStreaming = false };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<IOException>();
        observer.Events.OfType<ModelResponseFailed>().Count().ShouldBe(1);
        _ = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
    }

    /// <summary>Verifies an event stream that ends after a completed text block settles with that text rather than an empty terminal.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenStreamFailsAfterPartialText_RetainsPartialPartsAndUsage()
    {
        // messageStart, contentBlockStart, two deltas, contentBlockStop: the block is complete but no messageStop or metadata follows.
        var truncated = TakeLeadingFrames(TestResources.ReadAllBytes("responses/streaming_text.bin"), frameCount: 5);
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(truncated)) });
        var options = new AwsBedrockProviderOptions { Region = "us-east-1", PreferStreaming = true };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello world");
        // Converse reports usage only in the trailing metadata event, so an interrupted stream truthfully has none.
        failed.Usage.ShouldBeNull();
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
        terminal.PartialParts.ShouldBe(failed.PartialParts);
        terminal.Usage.ShouldBeNull();
    }

    /// <summary>Verifies a transport fault after a completed block reports the parts the observer already saw completed.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenBodyStreamFaultsAfterCompletedPart_RetainsCompletedPartsInFailure()
    {
        var prefix = TakeLeadingFrames(TestResources.ReadAllBytes("responses/streaming_text.bin"), frameCount: 5);
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(FaultingReadStream.ConnectionReset(prefix)) });
        var options = new AwsBedrockProviderOptions { Region = "us-east-1", PreferStreaming = true };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        var observer = new RecordingModelResponseObserver();

        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failed.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello world");
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseFailed>();
        terminal.PartialParts.ShouldBe(failed.PartialParts);
        observer.Events.Select(static e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(static i => (long) i));
    }

    /// <summary>Verifies the terminal cancellation event is delivered even to an observer that rejects the caller's cancelled token.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenCancelledMidStream_DeliversTerminalEventWithCancellationTokenNone()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/streaming_text.bin", "application/vnd.amazon.eventstream");
        var options = new AwsBedrockProviderOptions { Region = "us-east-1", PreferStreaming = true };
        var model = CreateModel(handler, CreateCredentials(), options: options);
        using var cts = new CancellationTokenSource();
        // Started, PartStarted, two deltas, PartCompleted: cancel once the text block has been completed.
        var observer = new TokenHonouringModelResponseObserver(cts, cancelAfterEventCount: 5);

        var result = await model.ExecuteAsync(CreateRequest(TestModels.ClaudeSonnet, Now.AddMinutes(1)), observer, cts.Token);

        var cancelled = result.ShouldBeOfType<ModelAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        var terminal = observer.Events[^1].ShouldBeOfType<ModelResponseCancelled>();
        cancelled.PartialParts.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text.ShouldBe("Hello world");
        terminal.PartialParts.ShouldBe(cancelled.PartialParts);
        observer.Events.ShouldNotContain(e => e is ModelResponseCompleted);
        observer.Events.Select(static e => e.Sequence).ShouldBe(Enumerable.Range(0, observer.Events.Count).Select(static i => (long) i));
    }

    /// <summary>Returns the first <paramref name="frameCount"/> complete AWS event-stream frames of a recorded body, using each frame's big-endian total-length prelude.</summary>
    private static byte[] TakeLeadingFrames(byte[] payload, int frameCount)
    {
        var end = 0;
        for (var frame = 0; frame < frameCount; frame++)
        {
            end += System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(end, 4));
        }

        return payload[..end];
    }
}
