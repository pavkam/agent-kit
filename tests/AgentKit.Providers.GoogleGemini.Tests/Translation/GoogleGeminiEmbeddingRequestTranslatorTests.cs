// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests.Translation;

/// <summary>
/// Verifies that <see cref="GoogleGeminiEmbeddingRequestTranslator"/>
/// produces the exact Gemini <c>batchEmbedContents</c> request body
/// expected for representative provider-neutral requests, comparing
/// against JSON fixture files rather than JSON literals embedded in test
/// source.
/// </summary>
public sealed class GoogleGeminiEmbeddingRequestTranslatorTests
{
    private static EmbeddingModelRequest CreateRequest(EmbeddingRequest embeddingRequest) =>
        new(
            new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), TestModels.TextEmbedding004, embeddingRequest),
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

        var actual = new GoogleGeminiEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));
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

        var actual = new GoogleGeminiEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));
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
    public void Translate_WhenPurposeSpecified_MapsToGeminiTaskType(EmbeddingPurpose purpose, string expectedTaskType)
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], purpose, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);

        var body = new GoogleGeminiEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));

        body["requests"]![0]!["taskType"]!.GetValue<string>().ShouldBe(expectedTaskType);
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

        var body = new GoogleGeminiEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));

        body["requests"]![0]!["autoTruncate"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Theory]
    [InlineData(EmbeddingTruncation.Start)]
    [InlineData(EmbeddingTruncation.End)]
    public void Translate_WhenTruncationIsDirectional_ThrowsNotSupportedException(EmbeddingTruncation truncation)
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Unspecified, null, null, truncation, ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new GoogleGeminiEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest)));
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
            () => new GoogleGeminiEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest)));
    }

    [Fact]
    public void Translate_WhenTruncationIsUndefined_ThrowsNotSupportedException()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Unspecified, null, null, (EmbeddingTruncation) 999, ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new GoogleGeminiEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest)));
    }

    [Fact]
    public void Translate_WhenPurposeIsUndefined_ThrowsNotSupportedException()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], (EmbeddingPurpose) 999, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new GoogleGeminiEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest)));
    }

    [Fact]
    public void Translate_WhenExtensionsAttemptToOverrideProtectedField_IgnoresOverride()
    {
        var hackedRequests = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(Array.Empty<object>())]);
        var extensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("requests", hackedRequests));

        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Unspecified, null, null, EmbeddingTruncation.ProviderDefault, extensions);

        var body = new GoogleGeminiEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));

        body["requests"]!.AsArray().Count.ShouldBe(1);
    }
}
