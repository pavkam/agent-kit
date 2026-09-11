// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI.Tests;

using System.Net;

using AgentKit.Providers.AzureOpenAI.Tests.Fakes;

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
}
