// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI.Tests;

using System.Net;
using System.Net.Http;

using AgentKit.Providers.AzureOpenAI.Tests.Fakes;
using AgentKit.TestSupport;

/// <summary>Verifies AzureOpenAIEmbeddingModel behavior and contracts.</summary>
public sealed class AzureOpenAIEmbeddingModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static EmbeddingModelDescriptor CreateDescriptor() => new(new EmbeddingModelAlias("embed"), AzureOpenAIProviderDefaults.ProviderId, AzureOpenAIProviderDefaults.EmbeddingApiFamily, new ModelId("text-embedding-3-small"), new DeploymentId("prod-embed"), AzureOpenAIProviderDefaults.DefaultEmbeddingCapabilities, AzureOpenAIProviderDefaults.DefaultEmbeddingLimits, pricing: null, ExtensionData.Empty);
    private static EmbeddingModelRequest CreateRequest(EmbeddingModelDescriptor descriptor) => new(new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), descriptor, new EmbeddingRequest([new TextEmbeddingInput("hello world", null)], EmbeddingPurpose.Unspecified, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty)), attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);
    private static AzureOpenAIEmbeddingModel CreateModel(StubHttpMessageHandler handler, IProviderCredentialSource credentials, EmbeddingModelDescriptor descriptor) => new(descriptor, AzureOpenAIProviderDefaults.CreateProfile(new AzureOpenAIProviderOptions { ResourceEndpoint = new Uri("https://my-resource.openai.azure.test/"), }), new OpenAIEmbeddingRequestTranslator(), new OpenAIEmbeddingResponseParser(), credentials, new HttpClient(handler), new FakeTimeProvider(Now));
    [Fact]
    public async Task GenerateAsync_WhenUsingApiKeyCredential_SendsApiKeyHeaderAndOverridesModelFieldWithDeploymentName()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_success.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);
        var result = await model.GenerateAsync(CreateRequest(descriptor), TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var vector = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>();
        vector.Values.ShouldBe([0.7f, 0.8f, 0.9f]);
        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.RequestUri.ShouldBe(new Uri("https://my-resource.openai.azure.test/openai/v1/embeddings"));
        sentRequest.Headers.Contains("api-key").ShouldBeTrue();
        sentRequest.Headers.GetValues("api-key").ShouldContain("azure-resource-key");
        sentRequest.Headers.Authorization.ShouldBeNull();
        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["model"]!.GetValue<string>().ShouldBe("prod-embed");
    }

    [Fact]
    public async Task GenerateAsync_WhenUnauthorized_ReturnsAuthenticationFailure()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.Unauthorized, "responses/error_401.json");
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("bad-key"), descriptor);
        var result = await model.GenerateAsync(CreateRequest(descriptor), TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
    }

    /// <summary>Verifies the transport's own timeout is a typed timeout failure, never an escaping exception or a caller cancellation.</summary>
    [Fact]
    public async Task GenerateAsync_WhenHttpClientTimeoutFiresWithoutCallerCancellation_ReturnsTypedTimeoutFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.",
            new TimeoutException("The operation was canceled.")));
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);

        var result = await model.GenerateAsync(CreateRequest(descriptor), TestContext.Current.CancellationToken);

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
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);

        var result = await model.GenerateAsync(CreateRequest(descriptor), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<HttpRequestException>();
    }

    /// <summary>Verifies caller cancellation while reading an error body returns one cancellation outcome that keeps the HTTP evidence.</summary>
    [Fact]
    public async Task GenerateAsync_WhenCallerCancelsDuringErrorBodyRead_ReturnsCancelledResult()
    {
        var body = new GatedReadStream();
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StreamContent(body)
            };
            response.Headers.Add("x-request-id", "req_cancelled");
            return response;
        });
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);
        using var cancellation = new CancellationTokenSource();

        var pending = model.GenerateAsync(CreateRequest(descriptor), cancellation.Token);
        (await Task.WhenAny(body.Entered, pending)).ShouldBe(body.Entered);
        await cancellation.CancelAsync();
        var result = await pending;

        var cancelled = result.ShouldBeOfType<EmbeddingAttemptCancelled>().Cancellation;
        cancelled.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        cancelled.StatusCode.ShouldBe(429);
        cancelled.RequestId.ShouldBe(new ProviderRequestId("req_cancelled"));
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
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);

        var result = await model.GenerateAsync(CreateRequest(descriptor), TestContext.Current.CancellationToken);

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
            Content = new StreamContent(FaultingReadStream.ConnectionReset("{\"object\":\"list\",\"data\":["u8.ToArray())),
        });
        var descriptor = CreateDescriptor();
        var model = CreateModel(handler, new StaticApiKeyCredentialSource("azure-resource-key"), descriptor);

        var result = await model.GenerateAsync(CreateRequest(descriptor), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Unavailable);
        _ = failed.Failure.DiagnosticCause.ShouldBeOfType<IOException>();
    }
}
