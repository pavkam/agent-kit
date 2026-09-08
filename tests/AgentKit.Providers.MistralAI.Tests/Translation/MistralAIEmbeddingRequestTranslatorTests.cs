// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Tests.Translation;

/// <summary>
/// Verifies that <see cref="MistralAIEmbeddingRequestTranslator"/> produces
/// the exact Mistral AI embeddings request body expected for
/// representative provider-neutral requests, comparing against JSON
/// fixture files rather than JSON literals embedded in test source.
/// </summary>
public sealed class MistralAIEmbeddingRequestTranslatorTests
{
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

    private static EmbeddingModelRequest CreateRequest(EmbeddingRequest embeddingRequest) =>
        new(
            new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), CreateDescriptor(), embeddingRequest),
            attempt: 1,
            DateTimeOffset.UtcNow.AddMinutes(1),
            ProviderRequestOptions.Empty);

    [Fact]
    public void Translate_WhenSingleInputWithDimensionsAndFloatEncoding_MatchesExpectedRequestBody()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hello world", null)],
            EmbeddingPurpose.Unspecified,
            dimensions: 512,
            encoding: EmbeddingEncoding.Float,
            EmbeddingTruncation.ProviderDefault,
            ExtensionData.Empty);

        var actual = new MistralAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));
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

        var actual = new MistralAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/embedding_request_batch.json"));

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Theory]
    [InlineData(EmbeddingEncoding.Float, "float")]
    [InlineData(EmbeddingEncoding.Int8, "int8")]
    [InlineData(EmbeddingEncoding.UInt8, "uint8")]
    [InlineData(EmbeddingEncoding.Binary, "binary")]
    [InlineData(EmbeddingEncoding.UBinary, "ubinary")]
    public void Translate_WhenEncodingSpecified_MapsToOutputDtype(EmbeddingEncoding encoding, string expectedDtype)
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Unspecified, null, encoding, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);

        var body = new MistralAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest));

        body["output_dtype"]!.GetValue<string>().ShouldBe(expectedDtype);
    }

    [Fact]
    public void Translate_WhenPurposeSpecified_ThrowsNotSupportedException()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Document, null, null, EmbeddingTruncation.ProviderDefault, ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new MistralAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest)));
    }

    [Fact]
    public void Translate_WhenTruncationSpecified_ThrowsNotSupportedException()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)], EmbeddingPurpose.Unspecified, null, null, EmbeddingTruncation.Reject, ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new MistralAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest)));
    }
}
