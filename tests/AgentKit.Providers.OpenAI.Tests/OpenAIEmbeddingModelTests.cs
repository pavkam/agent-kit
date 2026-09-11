// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI.Tests;

using System.Net;

using AgentKit.Providers.OpenAI.Tests.Fakes;

/// <summary>Verifies OpenAIEmbeddingModel behavior and contracts.</summary>
public sealed class OpenAIEmbeddingModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static EmbeddingModelDescriptor CreateDescriptor() => new(new EmbeddingModelAlias("embed"), OpenAIProviderDefaults.ProviderId, OpenAIProviderDefaults.EmbeddingApiFamily, new ModelId("text-embedding-3-small"), deploymentId: null, OpenAIProviderDefaults.DefaultEmbeddingCapabilities, OpenAIProviderDefaults.DefaultEmbeddingLimits, pricing: null, ExtensionData.Empty);
    private static EmbeddingModelRequest CreateRequest(EmbeddingModelDescriptor descriptor) => new(new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), descriptor, new EmbeddingRequest([new TextEmbeddingInput("hello world", null)], EmbeddingPurpose.Unspecified, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty)), attempt: 1, Now.AddMinutes(1), ProviderRequestOptions.Empty);
    [Fact]
    public async Task GenerateAsync_WhenUsingApiKeyCredential_SendsBearerHeaderAndReturnsCompletedResponse()
    {
        var handler = StubHttpMessageHandler.FromFixture(HttpStatusCode.OK, "responses/embedding_success.json");
        var options = new OpenAIProviderOptions();
        var descriptor = CreateDescriptor();
        var model = new OpenAIEmbeddingModel(descriptor, OpenAIProviderDefaults.CreateProfile(options), new OpenAIEmbeddingRequestTranslator(), new OpenAIEmbeddingResponseParser(), new StaticApiKeyCredentialSource("sk-real-looking-key"), new HttpClient(handler), new FakeTimeProvider(Now));
        var result = await model.GenerateAsync(CreateRequest(descriptor), TestContext.Current.CancellationToken);
        var completed = result.ShouldBeOfType<EmbeddingAttemptCompleted>();
        var vector = completed.Response.Items[0].ShouldBeOfType<EmbeddingItemSucceeded>().Vector.ShouldBeOfType<DenseFloatVector>();
        vector.Values.ShouldBe([0.1f, 0.2f, 0.3f]);
        _ = handler.Requests.ShouldHaveSingleItem();
        handler.Requests[0].RequestUri.ShouldBe(new Uri("https://api.openai.com/v1/embeddings"));
        handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("sk-real-looking-key");
    }
}
