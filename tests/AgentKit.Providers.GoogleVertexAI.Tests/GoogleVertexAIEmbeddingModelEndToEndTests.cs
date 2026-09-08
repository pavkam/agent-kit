// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Tests;

using System.Net;

using AgentKit.Providers.GoogleVertexAI.Tests.Fakes;

/// <summary>
/// End-to-end tests for the concrete <see cref="GoogleVertexAIEmbeddingModel"/>,
/// constructed directly (bypassing dependency injection) against a stub
/// HTTP handler serving fixture payloads. No test in this class performs a
/// real network call to Vertex AI.
/// </summary>
public sealed class GoogleVertexAIEmbeddingModelEndToEndTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static EmbeddingModelDescriptor CreateDescriptor(DeploymentId? deploymentId = null) =>
        new(
            new EmbeddingModelAlias("embed"),
            GoogleVertexAIProviderDefaults.ProviderId,
            GoogleVertexAIProviderDefaults.EmbeddingApiFamily,
            new ModelId("text-embedding-005"),
            deploymentId,
            GoogleVertexAIProviderDefaults.DefaultEmbeddingCapabilities,
            GoogleVertexAIProviderDefaults.DefaultEmbeddingLimits,
            pricing: null,
            ExtensionData.Empty);

    private static EmbeddingModelRequest CreateRequest(EmbeddingModelDescriptor descriptor, DateTimeOffset deadline) =>
        new(
            new EmbeddingRequestContext(
                new EmbeddingRequestId(Guid.NewGuid()),
                descriptor,
                new EmbeddingRequest(
                    [new TextEmbeddingInput("hello world", null)],
                    EmbeddingPurpose.Unspecified,
                    null,
                    null,
                    EmbeddingTruncation.ProviderDefault,
                    ExtensionData.Empty)),
            attempt: 1,
            deadline,
            ProviderRequestOptions.Empty);

    private static GoogleVertexAIEmbeddingModel CreateModel(
        StubHttpMessageHandler handler,
        IProviderCredentialSource credentials,
        EmbeddingModelDescriptor descriptor) =>
        new(
            descriptor,
            new GoogleVertexAIProviderOptions { ProjectId = "my-project", Location = "us-central1" },
            new GoogleVertexAIEmbeddingRequestTranslator(),
            new GoogleVertexAIEmbeddingResponseParser(),
            credentials,
            new HttpClient(handler),
            new FakeTimeProvider(Now));

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
        sentRequest.RequestUri.ShouldBe(new Uri(
            "https://us-central1-aiplatform.googleapis.com/v1/projects/my-project/locations/us-central1/" +
            "publishers/google/models/text-embedding-005:predict"));
        sentRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        sentRequest.Headers.Authorization!.Parameter.ShouldBe("gcp-access-token");
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

        handler.Requests[0].RequestUri.ShouldBe(new Uri(
            "https://us-central1-aiplatform.googleapis.com/v1/projects/my-project/locations/us-central1/" +
            "endpoints/my-endpoint:predict"));
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
}
