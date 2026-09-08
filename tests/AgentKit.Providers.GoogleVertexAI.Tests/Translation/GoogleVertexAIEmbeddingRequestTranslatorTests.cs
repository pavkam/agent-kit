// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Tests.Translation;

/// <summary>
/// Verifies that <see cref="GoogleVertexAIEmbeddingRequestTranslator"/>
/// produces the exact Vertex AI <c>:predict</c> request body expected for
/// representative provider-neutral requests, comparing against JSON
/// fixture files rather than JSON literals embedded in test source.
/// </summary>
public sealed class GoogleVertexAIEmbeddingRequestTranslatorTests
{
    private static EmbeddingModelDescriptor CreateDescriptor() => new(
        new EmbeddingModelAlias("embed"),
        GoogleVertexAIProviderDefaults.ProviderId,
        GoogleVertexAIProviderDefaults.EmbeddingApiFamily,
        new ModelId("text-embedding-005"),
        deploymentId: null,
        GoogleVertexAIProviderDefaults.DefaultEmbeddingCapabilities,
        GoogleVertexAIProviderDefaults.DefaultEmbeddingLimits,
        pricing: null,
        ExtensionData.Empty);

    private static EmbeddingModelRequest CreateRequest(EmbeddingRequest embeddingRequest) =>
        new(
            new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), CreateDescriptor(), embeddingRequest),
            attempt: 1,
            DateTimeOffset.UtcNow.AddMinutes(1),
            ProviderRequestOptions.Empty);

    [Fact]
    public void Translate_WhenSingleInputWithPurposeAndDimensions_MatchesExpectedRequestBody()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hello world", null)],
            EmbeddingPurpose.Document,
            dimensions: 256,
            encoding: null,
            EmbeddingTruncation.ProviderDefault,
            ExtensionData.Empty);

        var actual = new GoogleVertexAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/embedding_request.json"));

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Translate_WhenMultipleInputsAndNoOptions_MatchesExpectedRequestBody()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("first", null), new TextEmbeddingInput("second", null)],
            EmbeddingPurpose.Unspecified,
            dimensions: null,
            encoding: null,
            EmbeddingTruncation.ProviderDefault,
            ExtensionData.Empty);

        var actual = new GoogleVertexAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/embedding_request_batch.json"));

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Theory]
    [InlineData(EmbeddingPurpose.Query, "RETRIEVAL_QUERY")]
    [InlineData(EmbeddingPurpose.Document, "RETRIEVAL_DOCUMENT")]
    [InlineData(EmbeddingPurpose.Similarity, "SEMANTIC_SIMILARITY")]
    [InlineData(EmbeddingPurpose.Classification, "CLASSIFICATION")]
    [InlineData(EmbeddingPurpose.Clustering, "CLUSTERING")]
    [InlineData(EmbeddingPurpose.QuestionAnswering, "QUESTION_ANSWERING")]
    [InlineData(EmbeddingPurpose.CodeRetrieval, "CODE_RETRIEVAL_QUERY")]
    public void Translate_WhenPurposeSpecified_MapsToVertexTaskType(EmbeddingPurpose purpose, string expectedTaskType)
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], purpose, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);

        var body = new GoogleVertexAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));

        body["instances"]![0]!["task_type"]!.GetValue<string>().ShouldBe(expectedTaskType);
    }

    [Fact]
    public void Translate_WhenTruncationIsReject_SerializesAutoTruncateFalse()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)],
            EmbeddingPurpose.Unspecified,
            null,
            null,
            EmbeddingTruncation.Reject,
            ExtensionData.Empty);

        var body = new GoogleVertexAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));

        body["parameters"]!["autoTruncate"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Theory]
    [InlineData(EmbeddingTruncation.Start)]
    [InlineData(EmbeddingTruncation.End)]
    public void Translate_WhenTruncationIsDirectional_ThrowsNotSupportedException(EmbeddingTruncation truncation)
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Unspecified, null, null, truncation, ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new GoogleVertexAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest)));
    }

    [Theory]
    [InlineData(EmbeddingEncoding.Int8)]
    [InlineData(EmbeddingEncoding.UInt8)]
    [InlineData(EmbeddingEncoding.Binary)]
    [InlineData(EmbeddingEncoding.UBinary)]
    public void Translate_WhenUnsupportedEncoding_ThrowsNotSupportedException(EmbeddingEncoding encoding)
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Unspecified, null, encoding, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new GoogleVertexAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest)));
    }
}
