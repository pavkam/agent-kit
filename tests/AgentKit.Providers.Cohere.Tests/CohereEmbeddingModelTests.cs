// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Tests;

using System.Net;
using System.Net.Http;

using AgentKit.Providers.Cohere.Tests.Fakes;

/// <summary>Verifies CohereEmbeddingModel behavior and contracts.</summary>
public sealed class CohereEmbeddingModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static EmbeddingModelDescriptor CreateDescriptor() => new(new EmbeddingModelAlias("embed"), CohereProviderDefaults.ProviderId, CohereProviderDefaults.EmbeddingApiFamily, new ModelId("embed-v4.0"), deploymentId: null, CohereProviderDefaults.DefaultEmbeddingCapabilities, CohereProviderDefaults.DefaultEmbeddingLimits, pricing: null, ExtensionData.Empty);
    private static EmbeddingModelRequest CreateRequest(EmbeddingModelDescriptor descriptor, DateTimeOffset deadline) => new(new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), descriptor, new EmbeddingRequest([new TextEmbeddingInput("hello", null), new TextEmbeddingInput("world", null)], EmbeddingPurpose.Document, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty)), attempt: 1, deadline, ProviderRequestOptions.Empty);
    private static CohereEmbeddingModel CreateModel(HttpMessageHandler handler, IProviderCredentialSource credentials, CohereProviderOptions? options = null) => new(CreateDescriptor(), options ?? new CohereProviderOptions { BaseAddress = new Uri("https://api.cohere.test/") }, new CohereEmbeddingRequestTranslator(), new CohereEmbeddingResponseParser(), credentials, new HttpClient(handler), new FakeTimeProvider(Now));
    [Fact]
    public async Task GenerateAsync_WhenUsingApiKeyCredential_SendsBearerHeaderAndReturnsCompletedResponse()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")));
        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        completed.Response.Items.Length.ShouldBe(2);
        var vector = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>();
        vector.Values.ShouldBe([0.1f, 0.2f, 0.3f]);
        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.RequestUri.ShouldBe(new Uri("https://api.cohere.test/v2/embed"));
        sentRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        sentRequest.Headers.Authorization!.Parameter.ShouldBe("cohere-test-key");
    }

    [Fact]
    public async Task GenerateAsync_WhenDeadlineAlreadyElapsed_FailsWithTimeoutWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")));
        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddSeconds(-1)), TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WhenPurposeIsUnspecified_FailsWithInvalidRequestWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response_float.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("cohere-test-key")));
        var descriptor = CreateDescriptor();
        var request = new EmbeddingModelRequest(new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), descriptor, new EmbeddingRequest([new TextEmbeddingInput("hello", null)], EmbeddingPurpose.Unspecified, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty)), attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);
        var result = await model.GenerateAsync(request, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WhenHttpErrorStatus_FailsWithMappedFailureKind()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.Unauthorized, "responses/error_401.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("bad-key")));
        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddMinutes(1)), TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
    }
}
