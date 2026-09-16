// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Tests;

using System.Net;
using System.Net.Http;
using System.Text;

using AgentKit.Providers.GoogleVertexAI.Tests.Fakes;
using AgentKit.Providers.Http;
using AgentKit.TestSupport;

/// <summary>Verifies GoogleVertexAIEmbeddingModel behavior and contracts.</summary>
public sealed class GoogleVertexAIEmbeddingModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static EmbeddingModelDescriptor CreateDescriptor(DeploymentId? deploymentId = null) => new(new EmbeddingModelAlias("embed"), GoogleVertexAIProviderDefaults.ProviderId, GoogleVertexAIProviderDefaults.EmbeddingApiFamily, new ModelId("text-embedding-005"), deploymentId, GoogleVertexAIProviderDefaults.DefaultEmbeddingCapabilities, GoogleVertexAIProviderDefaults.DefaultEmbeddingLimits, pricing: null, ExtensionData.Empty);
    private static EmbeddingModelRequest CreateRequest(EmbeddingModelDescriptor descriptor, DateTimeOffset deadline) => new(new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), descriptor, new EmbeddingRequest([new TextEmbeddingInput("hello world", null)], EmbeddingPurpose.Unspecified, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty)), attempt: 1, deadline, ProviderRequestOptions.Empty);
    private static GoogleVertexAIEmbeddingModel CreateModel(HttpMessageHandler handler, IProviderCredentialSource credentials, EmbeddingModelDescriptor descriptor, TimeProvider? timeProvider = null) => new(descriptor, new GoogleVertexAIProviderOptions { ProjectId = "my-project", Location = "us-central1" }, new GoogleVertexAIEmbeddingRequestTranslator(), new GoogleVertexAIEmbeddingResponseParser(), credentials, new HttpClient(handler), timeProvider ?? new FakeTimeProvider(Now));
    [Fact]
    public async Task GenerateAsync_WhenUsingOAuthCredential_SendsBearerHeaderAndPublisherModelUri()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("gcp-access-token", null)), descriptor);
        var result = await model.GenerateAsync(CreateRequest(descriptor, Now.AddMinutes(1)), TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>().Values.ShouldBe([0.1f, 0.2f, 0.3f]);
        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.RequestUri.ShouldBe(new Uri("https://us-central1-aiplatform.googleapis.com/v1/projects/my-project/locations/us-central1/" + "publishers/google/models/text-embedding-005:predict"));
        sentRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        sentRequest.Headers.Authorization!.Parameter.ShouldBe("gcp-access-token");
    }

    [Fact]
    public async Task GenerateAsync_WhenRequestModelIdentityDiffersFromAdapter_FailsWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("gcp-access-token", null)), descriptor);
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
        const string hostileBody = /*lang=json,strict*/ """{ "error": { "code": 401, "message": "Authorization failed for sk-live-super-secret; internal tenant alice@example.test.", "status": "UNAUTHENTICATED" } }""";
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(hostileBody, Encoding.UTF8, "application/json"),
        });
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("gcp-access-token", null)), descriptor);

        var result = await model.GenerateAsync(CreateRequest(descriptor, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<EmbeddingAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failure.SafeMessage.ShouldBe("The provider returned HTTP status 401.");
        failure.SafeMessage.ShouldNotContain("sk-live-super-secret");
        failure.SafeMessage.ShouldNotContain("alice@example.test");
        failure.ProviderCode.ShouldBe("UNAUTHENTICATED");
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
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("gcp-access-token", null)), descriptor);

        var result = await model.GenerateAsync(CreateRequest(descriptor, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<EmbeddingAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failure.StatusCode.ShouldBe(502);
        failure.SafeMessage.ShouldBe("The provider returned HTTP status 502.");
        _ = failure.DiagnosticCause.ShouldBeOfType<JsonException>();
        ProviderErrorMessageEvidence.TryRead(failure.Extensions).ShouldBeNull();
    }

    [Fact]
    public async Task GenerateAsync_WhenApiKeyCredentialSupplied_FailsWithAuthenticationFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("not-supported")), descriptor);
        var result = await model.GenerateAsync(CreateRequest(descriptor, Now.AddMinutes(1)), TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WhenDeploymentIdSupplied_UsesEndpointResourceInstead()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response.json");
        var descriptor = CreateDescriptor(new DeploymentId("my-endpoint"));
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor);
        _ = await model.GenerateAsync(CreateRequest(descriptor, Now.AddMinutes(1)), TestContext.Current.CancellationToken);
        handler.Requests[0].RequestUri.ShouldBe(new Uri("https://us-central1-aiplatform.googleapis.com/v1/projects/my-project/locations/us-central1/" + "endpoints/my-endpoint:predict"));
    }

    [Fact]
    public async Task GenerateAsync_WhenDeadlineAlreadyElapsed_FailsWithTimeoutWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor);
        var result = await model.GenerateAsync(CreateRequest(descriptor, Now.AddSeconds(-1)), TestContext.Current.CancellationToken);
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
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor);

        var result = await model.GenerateAsync(CreateRequest(descriptor, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<TaskCanceledException>();
    }

    /// <summary>Verifies a refused connection is a typed unavailable failure.</summary>
    [Fact]
    public async Task GenerateAsync_WhenConnectionIsRefused_ReturnsTypedUnavailableFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Connection refused", new System.Net.Sockets.SocketException(61)));
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor);

        var result = await model.GenerateAsync(CreateRequest(descriptor, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

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
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor);
        using var cancellation = new CancellationTokenSource();

        var pending = model.GenerateAsync(CreateRequest(descriptor, Now.AddMinutes(1)), cancellation.Token);
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
            Content = new StreamContent(FaultingReadStream.ConnectionReset("{\"error\":"u8.ToArray()))
        });
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor);

        var result = await model.GenerateAsync(CreateRequest(descriptor, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<EmbeddingAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        failure.StatusCode.ShouldBe(500);
        failure.ProviderCode.ShouldBeNull();
        failure.SafeMessage.ShouldBe("The provider returned HTTP status 500.");
        _ = failure.DiagnosticCause.ShouldBeOfType<IOException>();
    }

    /// <summary>Verifies a connection reset while the body is being read is a typed unavailable failure.</summary>
    [Fact]
    public async Task GenerateAsync_WhenBodyStreamFailsMidRead_ReturnsTypedUnavailableFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(FaultingReadStream.ConnectionReset("{\"predictions\":["u8.ToArray())),
        });
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor);

        var result = await model.GenerateAsync(CreateRequest(descriptor, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<IOException>();
    }

    [Fact]
    public async Task GenerateAsync_WhenCallerCancelsBeforeCredentialResolution_ReturnsCancelledResult()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await model.GenerateAsync(CreateRequest(descriptor, Now.AddMinutes(1)), cts.Token);

        var cancelled = result.ShouldBeOfType<EmbeddingAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WhenTranslatorRejectsUnsupportedEncoding_ReturnsInvalidRequestFailureWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor);
        var context = new EmbeddingRequestContext(
            new EmbeddingRequestId(Guid.NewGuid()),
            descriptor,
            new EmbeddingRequest([new TextEmbeddingInput("hello world", null)], EmbeddingPurpose.Unspecified, null, EmbeddingEncoding.Int8, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty));
        var request = new EmbeddingModelRequest(context, attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);

        var result = await model.GenerateAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<NotSupportedException>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WhenCallerCancelsWhileSendIsInFlight_ReturnsCancelledResult()
    {
        var handler = new GatedSendHttpMessageHandler();
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor);
        using var cancellation = new CancellationTokenSource();

        var pending = model.GenerateAsync(CreateRequest(descriptor, Now.AddMinutes(1)), cancellation.Token);
        (await Task.WhenAny(handler.Entered, pending)).ShouldBe(handler.Entered);
        await cancellation.CancelAsync();
        var result = await pending;

        var cancelled = result.ShouldBeOfType<EmbeddingAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
    }

    [Fact]
    public async Task GenerateAsync_WhenDeadlineExpiresWhileSendIsInFlight_ReturnsTimeoutFailure()
    {
        var clock = new FakeTimeProvider(Now);
        var handler = new GatedSendHttpMessageHandler();
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor, timeProvider: clock);

        var pending = model.GenerateAsync(CreateRequest(descriptor, Now.AddSeconds(1)), TestContext.Current.CancellationToken);
        (await Task.WhenAny(handler.Entered, pending)).ShouldBe(handler.Entered);
        clock.Advance(TimeSpan.FromSeconds(2));
        var result = await pending;

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
    }

    [Fact]
    public async Task GenerateAsync_WhenDeadlineExpiresDuringErrorBodyRead_ReturnsTimeoutWithStatus()
    {
        var clock = new FakeTimeProvider(Now);
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StreamContent(body),
        });
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor, timeProvider: clock);

        var pending = model.GenerateAsync(CreateRequest(descriptor, Now.AddSeconds(1)), TestContext.Current.CancellationToken);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        clock.Advance(TimeSpan.FromSeconds(2));
        var result = await pending;

        var failure = result.ShouldBeOfType<EmbeddingAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failure.StatusCode.ShouldBe(503);
    }

    [Fact]
    public async Task GenerateAsync_WhenTransportTimesOutDuringErrorBodyRead_ReturnsTimeoutWithStatus()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StreamContent(new FaultingReadStream(static () => new TaskCanceledException("The transport timed out.", new TimeoutException()))),
        });
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor);

        var result = await model.GenerateAsync(CreateRequest(descriptor, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<EmbeddingAttemptFailed>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        failure.StatusCode.ShouldBe(502);
        _ = failure.DiagnosticCause.ShouldBeOfType<TaskCanceledException>();
    }

    [Fact]
    public async Task GenerateAsync_WhenCallerCancelsDuringSuccessBodyRead_ReturnsCancelledResult()
    {
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(body),
        });
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor);
        using var cancellation = new CancellationTokenSource();

        var pending = model.GenerateAsync(CreateRequest(descriptor, Now.AddMinutes(1)), cancellation.Token);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        await cancellation.CancelAsync();
        var result = await pending;

        var cancelled = result.ShouldBeOfType<EmbeddingAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
    }

    [Fact]
    public async Task GenerateAsync_WhenDeadlineExpiresDuringSuccessBodyRead_ReturnsTimeoutFailure()
    {
        var clock = new FakeTimeProvider(Now);
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(body),
        });
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor, timeProvider: clock);

        var pending = model.GenerateAsync(CreateRequest(descriptor, Now.AddSeconds(1)), TestContext.Current.CancellationToken);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        clock.Advance(TimeSpan.FromSeconds(2));
        var result = await pending;

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
    }

    [Fact]
    public async Task GenerateAsync_WhenTransportTimesOutDuringSuccessBodyRead_ReturnsTypedTimeoutFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new FaultingReadStream(static () => new TaskCanceledException("The transport timed out.", new TimeoutException()))),
        });
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticProviderCredentialSource(new OAuthTokenProviderCredential("token", null)), descriptor);

        var result = await model.GenerateAsync(CreateRequest(descriptor, Now.AddMinutes(1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<TaskCanceledException>();
    }
}
