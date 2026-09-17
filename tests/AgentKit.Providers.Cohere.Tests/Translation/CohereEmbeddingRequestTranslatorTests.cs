// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Tests.Translation;

using System.Diagnostics;

/// <summary>
/// Verifies that <see cref="CohereEmbeddingRequestTranslator"/> produces
/// the exact Cohere v2 embed request body expected for representative
/// provider-neutral requests, comparing against JSON fixture files rather
/// than JSON literals embedded in test source.
/// </summary>
public sealed class CohereEmbeddingRequestTranslatorTests
{
    private static EmbeddingModelDescriptor CreateDescriptor() => new(
        new EmbeddingModelAlias("embed"),
        CohereProviderDefaults.ProviderId,
        CohereProviderDefaults.EmbeddingApiFamily,
        new ModelId("embed-v4.0"),
        deploymentId: null,
        CohereProviderDefaults.DefaultEmbeddingCapabilities,
        CohereProviderDefaults.DefaultEmbeddingLimits,
        pricing: null,
        ExtensionData.Empty);

    private static EmbeddingModelRequest CreateRequest(EmbeddingRequest embeddingRequest) =>
        new(
            new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), CreateDescriptor(), embeddingRequest),
            attempt: 1,
            DateTimeOffset.UtcNow.AddMinutes(1),
            ProviderRequestOptions.Empty);

    [Fact]
    public void Translate_WhenSingleDocumentInputWithDimensions_MatchesExpectedRequestBody()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hello world", null)],
            EmbeddingPurpose.Document,
            dimensions: 512,
            encoding: EmbeddingEncoding.Float,
            EmbeddingTruncation.ProviderDefault,
            ExtensionData.Empty);

        var actual = new CohereEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/embedding_request.json"));

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Translate_WhenMultipleQueryInputsAndNoOptions_MatchesExpectedRequestBody()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("first", null), new TextEmbeddingInput("second", null)],
            EmbeddingPurpose.Query,
            dimensions: null,
            encoding: null,
            EmbeddingTruncation.ProviderDefault,
            ExtensionData.Empty);

        var actual = new CohereEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/embedding_request_batch.json"));

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Theory]
    [InlineData(EmbeddingPurpose.Query, "search_query")]
    [InlineData(EmbeddingPurpose.Document, "search_document")]
    [InlineData(EmbeddingPurpose.Classification, "classification")]
    [InlineData(EmbeddingPurpose.Clustering, "clustering")]
    public void Translate_WhenPurposeSpecified_MapsToInputType(EmbeddingPurpose purpose, string expectedInputType)
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], purpose, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);

        var body = new CohereEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));

        body["input_type"]!.GetValue<string>().ShouldBe(expectedInputType);
    }

    [Theory]
    [InlineData(EmbeddingPurpose.Similarity)]
    [InlineData(EmbeddingPurpose.QuestionAnswering)]
    [InlineData(EmbeddingPurpose.CodeRetrieval)]
    public void Translate_WhenPurposeHasNoCohereEquivalent_ThrowsNotSupportedException(EmbeddingPurpose purpose)
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], purpose, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new CohereEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest)));
    }

    [Fact]
    public void Translate_WhenPurposeUnspecified_ThrowsNotSupportedException()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Unspecified, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new CohereEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest)));
    }

    [Theory]
    [InlineData(EmbeddingEncoding.Float, "float")]
    [InlineData(EmbeddingEncoding.Int8, "int8")]
    [InlineData(EmbeddingEncoding.UInt8, "uint8")]
    [InlineData(EmbeddingEncoding.Binary, "binary")]
    [InlineData(EmbeddingEncoding.UBinary, "ubinary")]
    public void Translate_WhenEncodingSpecified_MapsToEmbeddingTypesEntry(EmbeddingEncoding encoding, string expectedType)
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Document, null, encoding, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);

        var body = new CohereEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));

        body["embedding_types"]!.AsArray().Single()!.GetValue<string>().ShouldBe(expectedType);
    }

    [Theory]
    [InlineData(EmbeddingTruncation.Reject, "NONE")]
    [InlineData(EmbeddingTruncation.Start, "START")]
    [InlineData(EmbeddingTruncation.End, "END")]
    public void Translate_WhenTruncationSpecified_MapsToTruncateField(EmbeddingTruncation truncation, string expectedValue)
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Document, null, null, truncation, ExtensionData.Empty);

        var body = new CohereEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));

        body["truncate"]!.GetValue<string>().ShouldBe(expectedValue);
    }

    [Fact]
    public void Translate_WhenTruncationIsProviderDefault_OmitsTruncateField()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Document, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);

        var body = new CohereEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));

        body.ContainsKey("truncate").ShouldBeFalse();
    }

    [Fact]
    public void Translate_WhenPurposeIsUndefined_ThrowsNotSupportedException()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], (EmbeddingPurpose) 999, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new CohereEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest)));
    }

    [Fact]
    public void Translate_WhenEncodingIsUndefined_ThrowsUnreachableException()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Document, null, (EmbeddingEncoding) 999, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);

        _ = Should.Throw<UnreachableException>(
            () => new CohereEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest)));
    }

    [Fact]
    public void Translate_WhenTruncationIsUndefined_ThrowsNotSupportedException()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Document, null, null, (EmbeddingTruncation) 999, ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new CohereEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest)));
    }

}
