// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Ollama.Tests;

using System.Net;

using AgentKit.Providers.Ollama.Tests.Fakes;

/// <summary>
/// End-to-end tests for the concrete <see cref="OllamaEmbeddingModel"/>,
/// constructed directly (bypassing dependency injection) against a stub
/// HTTP handler serving a fixture response. No test in this class performs
/// a real network call to Ollama.
/// </summary>
public sealed class OllamaEmbeddingModelEndToEndTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static EmbeddingModelDescriptor CreateDescriptor() =>
        new(
            new EmbeddingModelAlias("embed"),
            OllamaProviderDefaults.ProviderId,
            OllamaProviderDefaults.EmbeddingApiFamily,
            new ModelId("nomic-embed-text"),
            deploymentId: null,
            OllamaProviderDefaults.DefaultEmbeddingCapabilities,
            OllamaProviderDefaults.DefaultEmbeddingLimits,
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
    public async Task GenerateAsync_WhenSuccess_ReturnsCompletedResponseFromLocalOllamaEndpoint()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_success.json");
        var options = new OllamaProviderOptions();
        var descriptor = CreateDescriptor();

        var model = new OllamaEmbeddingModel(
            descriptor,
            OllamaProviderDefaults.CreateProfile(options),
            new OpenAIEmbeddingRequestTranslator(),
            new OpenAIEmbeddingResponseParser(),
            new StaticApiKeyCredentialSource("unused-local-key"),
            new HttpClient(handler),
            new FakeTimeProvider(Now));

        var result = await model.GenerateAsync(CreateRequest(descriptor), TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var vector = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>();
        vector.Values.ShouldBe([0.5f, 0.6f, 0.7f]);

        _ = handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].RequestUri.ShouldBe(new Uri("http://localhost:11434/v1/embeddings"));
    }
}
