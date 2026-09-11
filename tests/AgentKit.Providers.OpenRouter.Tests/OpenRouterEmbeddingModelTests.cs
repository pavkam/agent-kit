// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter.Tests;

using System.Net;

using AgentKit.Providers.OpenRouter.Tests.Fakes;

/// <summary>Verifies OpenRouterEmbeddingModel behavior and contracts.</summary>
public sealed class OpenRouterEmbeddingModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static EmbeddingModelDescriptor CreateDescriptor() => new(new EmbeddingModelAlias("embed"), OpenRouterProviderDefaults.ProviderId, OpenRouterProviderDefaults.EmbeddingApiFamily, new ModelId("openai/text-embedding-3-small"), deploymentId: null, OpenRouterProviderDefaults.DefaultEmbeddingCapabilities, OpenRouterProviderDefaults.DefaultEmbeddingLimits, pricing: null, ExtensionData.Empty);
    private static EmbeddingModelRequest CreateRequest(EmbeddingModelDescriptor descriptor, EmbeddingPurpose purpose = EmbeddingPurpose.Unspecified) => new(new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), descriptor, new EmbeddingRequest([new TextEmbeddingInput("hello world", null)], purpose, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty)), attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);
    [Fact]
    public async Task GenerateAsync_WhenUsingApiKeyCredential_SendsBearerHeaderAndReturnsCompletedResponse()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_success.json");
        var options = new OpenRouterProviderOptions();
        var descriptor = CreateDescriptor();
        var model = new OpenRouterEmbeddingModel(descriptor, OpenRouterProviderDefaults.CreateProfile(options), new OpenAIEmbeddingRequestTranslator(), new OpenAIEmbeddingResponseParser(), new StaticApiKeyCredentialSource("sk-or-real-looking-key"), new HttpClient(handler), new FakeTimeProvider(Now));
        var result = await model.GenerateAsync(CreateRequest(descriptor), TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var vector = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>();
        vector.Values.ShouldBe([0.4f, 0.5f, 0.6f]);
        _ = handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].RequestUri.ShouldBe(new Uri("https://openrouter.ai/api/v1/embeddings"));
        handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("sk-or-real-looking-key");
    }

    [Fact]
    public async Task GenerateAsync_WhenPurposeSpecified_SendsInputTypeField()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_success.json");
        var options = new OpenRouterProviderOptions();
        var descriptor = CreateDescriptor();
        var model = new OpenRouterEmbeddingModel(descriptor, OpenRouterProviderDefaults.CreateProfile(options), new OpenAIEmbeddingRequestTranslator(), new OpenAIEmbeddingResponseParser(), new StaticApiKeyCredentialSource("sk-or-real-looking-key"), new HttpClient(handler), new FakeTimeProvider(Now));
        _ = await model.GenerateAsync(CreateRequest(descriptor, EmbeddingPurpose.Document), TestContext.Current.CancellationToken);
        var sentBody = JsonNode.Parse(handler.RequestBodies[0]!);
        sentBody!["input_type"]!.GetValue<string>().ShouldBe("search_document");
    }
}
