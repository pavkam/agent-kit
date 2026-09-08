// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests;

using System.Net;
using System.Net.Http;

using AgentKit.Providers.MistralAI.Tests.Fakes;

/// <summary>
/// End-to-end tests for <see cref="MistralAIEmbeddingModel"/>, exercising
/// the full pipeline (credential resolution, translation, transport, and
/// response parsing) against a stub HTTP handler serving fixture payloads.
/// No test in this class performs a real network call.
/// </summary>
public sealed class MistralAIEmbeddingModelEndToEndTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static EmbeddingModelDescriptor CreateDescriptor() => new(
        new EmbeddingModelAlias("embed"),
        MistralAIProviderDefaults.ProviderId,
        MistralAIProviderDefaults.EmbeddingApiFamily,
        new ModelId("mistral-embed"),
        deploymentId: null,
        MistralAIProviderDefaults.DefaultEmbeddingCapabilities,
        MistralAIProviderDefaults.DefaultEmbeddingLimits,
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

    private static MistralAIEmbeddingModel CreateModel(
        HttpMessageHandler handler,
        IProviderCredentialSource credentials,
        MistralAIProviderOptions? options = null) =>
        new(
            CreateDescriptor(),
            options ?? new MistralAIProviderOptions { BaseAddress = new Uri("https://api.mistral.test/v1/") },
            new MistralAIEmbeddingRequestTranslator(),
            new MistralAIEmbeddingResponseParser(),
            credentials,
            new HttpClient(handler),
            new FakeTimeProvider(Now));

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
    public async Task GenerateAsync_WhenDeadlineAlreadyElapsed_FailsWithTimeoutWithoutSendingHttpRequest()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_success.json");
        var model = CreateModel(handler, new StaticProviderCredentialSource(new ApiKeyProviderCredential("mistral-test-key")));

        var result = await model.GenerateAsync(CreateRequest(CreateDescriptor(), Now.AddSeconds(-1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<EmbeddingAttemptFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        handler.Requests.ShouldBeEmpty();
    }
}
