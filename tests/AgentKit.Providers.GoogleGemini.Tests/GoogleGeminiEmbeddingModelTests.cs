// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests;

using System.Net;
using System.Net.Http;

using AgentKit.Providers.GoogleGemini.Tests.Fakes;

/// <summary>Verifies GoogleGeminiEmbeddingModel behavior and contracts.</summary>
public sealed class GoogleGeminiEmbeddingModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static EmbeddingModelRequest CreateRequest(EmbeddingModelDescriptor descriptor, DateTimeOffset deadline) => new(new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), descriptor, new EmbeddingRequest([new TextEmbeddingInput("hello world", null)], EmbeddingPurpose.Unspecified, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty)), attempt: 1, deadline, ProviderRequestOptions.Empty);
    private static GoogleGeminiEmbeddingModel CreateModel(HttpMessageHandler handler, IProviderCredentialSource credentials, GoogleGeminiProviderOptions? options = null) => new(TestModels.TextEmbedding004, options ?? new GoogleGeminiProviderOptions { BaseAddress = new Uri("https://generativelanguage.test/") }, new GoogleGeminiEmbeddingRequestTranslator(), new GoogleGeminiEmbeddingResponseParser(), credentials, new HttpClient(handler), new FakeTimeProvider(Now));
    [Fact]
    public async Task GenerateAsync_WhenUsingApiKeyCredential_SendsApiKeyHeaderAndReturnsCompletedResponse()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("gemini-test-key")));
        var result = await model.GenerateAsync(CreateRequest(TestModels.TextEmbedding004, Now.AddMinutes(1)), TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var vector = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>();
        vector.Values.ShouldBe([0.1f, 0.2f, 0.3f]);
        _ = handler.Requests.ShouldHaveSingleItem();
        var sentRequest = handler.Requests[0];
        sentRequest.RequestUri.ShouldBe(new Uri("https://generativelanguage.test/v1beta/models/text-embedding-004:batchEmbedContents"));
        sentRequest.Headers.GetValues("x-goog-api-key").ShouldContain("gemini-test-key");
    }

    [Fact]
    public async Task GenerateAsync_WhenDeadlineAlreadyElapsed_FailsWithTimeoutWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("gemini-test-key")));
        var result = await model.GenerateAsync(CreateRequest(TestModels.TextEmbedding004, Now.AddSeconds(-1)), TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateAsync_WhenCallerCancelsBeforeCredentialResolution_ReturnsCancelledResult()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_response.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("gemini-test-key")));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var result = await model.GenerateAsync(CreateRequest(TestModels.TextEmbedding004, Now.AddMinutes(1)), cts.Token);
        var cancelled = result.ShouldBeOfType<EmbeddingAttemptCancelled>();
        cancelled.Cancellation.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        handler.Requests.ShouldBeEmpty();
    }
}
