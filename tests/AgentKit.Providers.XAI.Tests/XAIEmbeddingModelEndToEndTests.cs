// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.XAI.Tests;

using System.Net;

using AgentKit.Providers.XAI.Tests.Fakes;

/// <summary>
/// End-to-end tests for the concrete <see cref="XAIEmbeddingModel"/>,
/// constructed directly (bypassing dependency injection) against a stub
/// HTTP handler serving a fixture response. No test in this class performs
/// a real network call to xAI.
/// </summary>
public sealed class XAIEmbeddingModelEndToEndTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static EmbeddingModelDescriptor CreateDescriptor() =>
        new(
            new EmbeddingModelAlias("embed"),
            XAIProviderDefaults.ProviderId,
            XAIProviderDefaults.EmbeddingApiFamily,
            new ModelId("xai-embed-1"),
            deploymentId: null,
            XAIProviderDefaults.DefaultEmbeddingCapabilities,
            XAIProviderDefaults.DefaultEmbeddingLimits,
            pricing: null,
            ExtensionData.Empty);

    private static EmbeddingModelRequest CreateRequest(EmbeddingModelDescriptor descriptor) =>
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
            Now.AddMinutes(1),
            ProviderRequestOptions.Empty);

    [Fact]
    public async Task GenerateAsync_WhenUsingApiKeyCredential_SendsBearerHeaderAndReturnsCompletedResponse()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_success.json");
        var options = new XAIProviderOptions();
        var descriptor = CreateDescriptor();

        var model = new XAIEmbeddingModel(
            descriptor,
            XAIProviderDefaults.CreateProfile(options),
            new OpenAICompatible.OpenAIEmbeddingRequestTranslator(),
            new OpenAICompatible.OpenAIEmbeddingResponseParser(),
            new StaticApiKeyCredentialSource("xai-real-looking-key"),
            new HttpClient(handler),
            new FakeTimeProvider(Now));

        var result = await model.GenerateAsync(CreateRequest(descriptor), TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var vector = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>();
        vector.Values.ShouldBe([0.2f, 0.3f, 0.4f]);

        _ = handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].RequestUri.ShouldBe(new Uri("https://api.x.ai/v1/embeddings"));
        handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("xai-real-looking-key");
    }
}
