// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests;

using System.Net;
using System.Net.Http;

using AgentKit.Providers.MistralAI.Tests.Fakes;
using AgentKit.TestSupport;

/// <summary>Verifies MistralAIEmbeddingModel behavior and contracts.</summary>
public sealed class MistralAIEmbeddingModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static EmbeddingModelDescriptor CreateDescriptor() => new(new EmbeddingModelAlias("embed"), MistralAIProviderDefaults.ProviderId, MistralAIProviderDefaults.EmbeddingApiFamily, new ModelId("mistral-embed"), deploymentId: null, MistralAIProviderDefaults.DefaultEmbeddingCapabilities, MistralAIProviderDefaults.DefaultEmbeddingLimits, pricing: null, ExtensionData.Empty);
    private static EmbeddingModelRequest CreateRequest(EmbeddingModelDescriptor descriptor, DateTimeOffset deadline) => new(new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), descriptor, new EmbeddingRequest([new TextEmbeddingInput("hello world", null)], EmbeddingPurpose.Unspecified, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty)), attempt: 1, deadline, ProviderRequestOptions.Empty);
    private static MistralAIEmbeddingModel CreateModel(HttpMessageHandler handler, IProviderCredentialSource credentials, MistralAIProviderOptions? options = null) => new(CreateDescriptor(), options ?? new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/") }, new MistralAIEmbeddingRequestTranslator(), new MistralAIEmbeddingResponseParser(), credentials, new HttpClient(handler), new FakeTimeProvider(Now));
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
}
