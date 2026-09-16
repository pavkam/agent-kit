// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests;

using System.Net;
using System.Net.Http;

using AgentKit.Providers.Http;
using AgentKit.Providers.MistralAI.Tests.Fakes;
using AgentKit.TestSupport;

/// <summary>Verifies MistralAIEmbeddingModel behavior and contracts.</summary>
public sealed class MistralAIEmbeddingModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static EmbeddingModelDescriptor CreateDescriptor() => new(new EmbeddingModelAlias("embed"), MistralAIProviderDefaults.ProviderId, MistralAIProviderDefaults.EmbeddingApiFamily, new ModelId("mistral-embed"), deploymentId: null, MistralAIProviderDefaults.DefaultEmbeddingCapabilities, MistralAIProviderDefaults.DefaultEmbeddingLimits, pricing: null, ExtensionData.Empty);
    private static EmbeddingModelRequest CreateRequest(EmbeddingModelDescriptor descriptor, DateTimeOffset deadline) => new(new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), descriptor, new EmbeddingRequest([new TextEmbeddingInput("hello world", null)], EmbeddingPurpose.Unspecified, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty)), attempt: 1, deadline, ProviderRequestOptions.Empty);
    private static MistralAIEmbeddingModel CreateModel(HttpMessageHandler handler, IProviderCredentialSource credentials, MistralAIProviderOptions? options = null, TimeProvider? timeProvider = null) => new(CreateDescriptor(), options ?? new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/") }, new MistralAIEmbeddingRequestTranslator(), new MistralAIEmbeddingResponseParser(), credentials, new HttpClient(handler), timeProvider ?? new FakeTimeProvider(Now));
    [Fact]
    public async Task GenerateAsync_WhenUsingApiKeyCredential_SendsBearerHeaderAndReturnsCompletedResponse()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_success.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var vector = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>();
        vector.Values.ShouldBe([0.4f, 0.5f, 0.6f]);
        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.RequestUri.ShouldBe(new Uri("https://api.mistral.test/v1/embeddings"));
        sentRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        sentRequest.Headers.Authorization!.Parameter.ShouldBe("mistral-test-key");
    }

    [Fact]
    public async Task GenerateAsync_WhenRequestModelIdentityDiffersFromAdapter_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_success.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        var requestDescriptor = descriptor with { ModelId = new ModelId("different-embedding-model") };

        var result = await model.GenerateAsync(CreateRequest(requestDescriptor, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failed.Failure.SafeMessage.ShouldBe("The request model descriptor does not match the configured adapter descriptor.");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WhenErrorBodyContainsHostileText_DoesNotExposeItAsSafeMessage()
    {
        const string hostileBody = /*lang=json,strict*/ """{ "detail": "Authorization failed for sk-live-super-secret; internal tenant alice@example.test." }""";
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(hostileBody, Encoding.UTF8, "application/json"),
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));

        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<EmbeddingAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failure.SafeMessage.ShouldBe("The provider returned HTTP status 401.");
        failure.SafeMessage.ShouldNotContain("sk-live-super-secret");
        failure.SafeMessage.ShouldNotContain("alice@example.test");
        failure.ProviderCode.ShouldBeNull();
        failure.DiagnosticCause.ShouldBeNull();
        ProviderErrorMessageEvidence.TryRead(failure.Extensions).ShouldBe("Authorization failed for sk-live-super-secret; internal tenant alice@example.test.");
    }

    [Fact]
    public async Task GenerateAsync_WhenErrorBodyIsNotJson_FallsBackToGenericSafeMessage()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>Bad Gateway</html>", Encoding.UTF8, "text/html"),
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));

        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<EmbeddingAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failure.StatusCode.ShouldBe(502);
        failure.SafeMessage.ShouldBe("The provider returned HTTP status 502.");
        _ = failure.DiagnosticCause.ShouldBeOfType<JsonException>();
        ProviderErrorMessageEvidence.TryRead(failure.Extensions).ShouldBeNull();
    }

    [Fact]
    public async Task GenerateAsync_WhenDeadlineAlreadyElapsed_FailsWithTimeoutWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_success.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddSeconds(-1)), TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies the transport's own timeout is a typed timeout failure, never an escaping exception or a caller cancellation.</summary>
    [Fact]
    public async Task GenerateAsync_WhenHttpClientTimeoutFiresWithoutCallerCancellation_ReturnsTypedTimeoutFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.",
            new TimeoutException("The operation was canceled.")));
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));

        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<TaskCanceledException>();
    }

    /// <summary>Verifies a refused connection is a typed unavailable failure.</summary>
    [Fact]
    public async Task GenerateAsync_WhenConnectionIsRefused_ReturnsTypedUnavailableFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Connection refused", new System.Net.Sockets.SocketException(61)));
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));

        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<HttpRequestException>();
    }

    /// <summary>Verifies caller cancellation while reading an error body returns one cancellation outcome that keeps the HTTP evidence.</summary>
    [Fact]
    public async Task GenerateAsync_WhenCallerCancelsDuringErrorBodyRead_ReturnsCancelledResult()
    {
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StreamContent(body)
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        using var cancellation = new CancellationTokenSource();

        var pending = model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), cancellation.Token);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        await cancellation.CancelAsync();
        var result = await pending;

        var cancelled = result.ShouldBeOfType<EmbeddingAttemptCancelled>().Cancellation;
        cancelled.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        cancelled.StatusCode.ShouldBe(429);
    }

    /// <summary>Verifies a connection fault while reading an error body still yields the status-mapped failure with the fault retained as diagnostics.</summary>
    [Fact]
    public async Task GenerateAsync_WhenErrorBodyReadFails_ReturnsStatusOnlyFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StreamContent(FaultingReadStream.ConnectionReset("{\"detail\":"u8.ToArray()))
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));

        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<EmbeddingAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failure.StatusCode.ShouldBe(500);
        failure.SafeMessage.ShouldBe("The provider returned HTTP status 500.");
        _ = failure.DiagnosticCause.ShouldBeOfType<IOException>();
    }

    /// <summary>Verifies a connection reset while the body is being read is a typed unavailable failure.</summary>
    [Fact]
    public async Task GenerateAsync_WhenBodyStreamFailsMidRead_ReturnsTypedUnavailableFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(FaultingReadStream.ConnectionReset("{\"data\":["u8.ToArray())),
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));

        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<IOException>();
    }

    /// <summary>Verifies an error body with no 'detail' field at all yields no provider error evidence rather than throwing.</summary>
    [Fact]
    public async Task GenerateAsync_WhenErrorBodyHasNoDetailField_ReturnsFailureWithNoProviderErrorEvidence()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(/*lang=json,strict*/ """{ "message": "unauthorized" }""", Encoding.UTF8, "application/json"),
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));

        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<EmbeddingAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        ProviderErrorMessageEvidence.TryRead(failure.Extensions).ShouldBeNull();
    }

    /// <summary>Verifies a 'detail' field of an unexpected JSON kind (neither string nor array) yields no provider error evidence rather than throwing.</summary>
    [Fact]
    public async Task GenerateAsync_WhenDetailFieldIsUnexpectedJsonKind_ReturnsFailureWithNoProviderErrorEvidence()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(/*lang=json,strict*/ """{ "detail": 42 }""", Encoding.UTF8, "application/json"),
        });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));

        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<EmbeddingAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        ProviderErrorMessageEvidence.TryRead(failure.Extensions).ShouldBeNull();
    }

    /// <summary>Verifies a validation error array is joined for diagnostics without ever becoming the caller-visible safe message.</summary>
    [Fact]
    public async Task GenerateAsync_WhenValidationErrorArray_JoinsFieldMessagesIntoDiagnosticEvidenceOnly()
    {
        var handler = StubHttpMessageHandler.FromFixture((HttpStatusCode) 422, "responses/error_422_validation.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));

        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failed.Failure.SafeMessage.ShouldBe("The provider returned HTTP status 422.");
        ProviderErrorMessageEvidence.TryRead(failed.Failure.Extensions).ShouldBe("Input should be 'system', 'user', 'assistant' or 'tool' Input should be less than or equal to 1.5");
    }

    /// <summary>Verifies an expired OAuth credential is a typed authentication failure without ever sending the HTTP request.</summary>
    [Fact]
    public async Task GenerateAsync_WhenOAuthTokenExpired_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_success.json");
        var expiredCredential = new OAuthTokenProviderCredential("expired-token", Now.AddMinutes(-5));
        var model = CreateModel(handler, new StaticProviderCredentialSource(expiredCredential));

        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies a request specifying a purpose fails with a typed invalid-request outcome, since Mistral's embeddings API has no purpose parameter.</summary>
    [Fact]
    public async Task GenerateAsync_WhenPurposeIsSpecified_FailsWithInvalidRequestWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_success.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        var descriptor = CreateDescriptor();
        var request = new EmbeddingModelRequest(new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), descriptor, new EmbeddingRequest([new TextEmbeddingInput("hello", null)], EmbeddingPurpose.Query, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty)), attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);

        var result = await model.GenerateAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failed.Failure.SafeMessage.ShouldBe("The request could not be translated for the Mistral AI embeddings wire format.");
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<NotSupportedException>();
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies caller cancellation before credential resolution returns one cancelled outcome without sending the HTTP request.</summary>
    [Fact]
    public async Task GenerateAsync_WhenCallerCancelsBeforeCredentialResolution_ReturnsCancelledResult()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_success.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), cts.Token);

        var cancelled = result.ShouldBeOfType<EmbeddingAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies caller cancellation while the request is still being sent (before any response headers arrive) returns one cancelled outcome.</summary>
    [Fact]
    public async Task GenerateAsync_WhenCallerCancelsWhileSendIsInFlight_ReturnsCancelledResult()
    {
        var handler = new GatedSendHttpMessageHandler();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        using var cancellation = new CancellationTokenSource();

        var pending = model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), cancellation.Token);
        (await Task.WhenAny(handler.Entered, pending)).ShouldBe(handler.Entered);
        await cancellation.CancelAsync();
        var result = await pending;

        var cancelled = result.ShouldBeOfType<EmbeddingAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
    }

    /// <summary>Verifies the request deadline elapsing while the request is still being sent is a typed timeout, not a caller cancellation.</summary>
    [Fact]
    public async Task GenerateAsync_WhenDeadlineElapsesWhileSendIsInFlight_ReturnsTypedTimeoutFailure()
    {
        var handler = new GatedSendHttpMessageHandler();
        var timeProvider = new FakeTimeProvider(Now);
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), timeProvider: timeProvider);

        var pending = model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddSeconds(5)), TestContext.Current.CancellationToken);
        await handler.Entered;
        timeProvider.Advance(TimeSpan.FromSeconds(6));
        var result = await pending;

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failed.Failure.SafeMessage.ShouldBe("The request did not complete before its deadline.");
    }

    /// <summary>Verifies the request deadline elapsing while the provider's error response body is still being received returns a typed timeout retaining the HTTP evidence.</summary>
    [Fact]
    public async Task GenerateAsync_WhenDeadlineElapsesDuringErrorBodyRead_ReturnsTypedTimeoutFailureRetainingHttpEvidence()
    {
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StreamContent(body) });
        var timeProvider = new FakeTimeProvider(Now);
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), timeProvider: timeProvider);

        var pending = model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddSeconds(5)), TestContext.Current.CancellationToken);
        await body.Entered;
        timeProvider.Advance(TimeSpan.FromSeconds(6));
        var result = await pending;

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failed.Failure.StatusCode.ShouldBe(429);
        failed.Failure.SafeMessage.ShouldBe("The provider error response was not received before the request deadline.");
    }

    /// <summary>Verifies a bare transport timeout (neither the caller nor the deadline) while reading the provider's error body still yields a typed timeout retaining the HTTP evidence.</summary>
    [Fact]
    public async Task GenerateAsync_WhenTransportTimesOutDuringErrorBodyRead_ReturnsTypedTimeoutFailureRetainingHttpEvidence()
    {
        var body = new FaultingReadStream(static () => new OperationCanceledException("The transport's own read timeout elapsed."));
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StreamContent(body) });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));

        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failed.Failure.StatusCode.ShouldBe(429);
        failed.Failure.SafeMessage.ShouldBe("The transport timed out while the provider error response was being received.");
    }

    /// <summary>Verifies caller cancellation while the successful response body is still streaming in returns one cancelled outcome.</summary>
    [Fact]
    public async Task GenerateAsync_WhenCallerCancelsDuringSuccessfulBodyRead_ReturnsCancelledResult()
    {
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));
        using var cancellation = new CancellationTokenSource();

        var pending = model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), cancellation.Token);
        await body.Entered;
        await cancellation.CancelAsync();
        var result = await pending;

        var cancelled = result.ShouldBeOfType<EmbeddingAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
    }

    /// <summary>Verifies the request deadline elapsing while the successful response body is still streaming in returns a typed timeout.</summary>
    [Fact]
    public async Task GenerateAsync_WhenDeadlineElapsesDuringSuccessfulBodyRead_ReturnsTypedTimeoutFailure()
    {
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });
        var timeProvider = new FakeTimeProvider(Now);
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")), timeProvider: timeProvider);

        var pending = model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddSeconds(5)), TestContext.Current.CancellationToken);
        await body.Entered;
        timeProvider.Advance(TimeSpan.FromSeconds(6));
        var result = await pending;

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failed.Failure.SafeMessage.ShouldBe("The response was not fully received before the request's deadline.");
    }

    /// <summary>Verifies a bare transport timeout (neither the caller nor the deadline) while reading the successful response body still yields a typed timeout.</summary>
    [Fact]
    public async Task GenerateAsync_WhenTransportTimesOutDuringSuccessfulBodyRead_ReturnsTypedTimeoutFailure()
    {
        var body = new FaultingReadStream(static () => new OperationCanceledException("The transport's own read timeout elapsed."));
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));

        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failed.Failure.SafeMessage.ShouldBe("The transport timed out while the response body was being received.");
    }
}
