// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Translation;

/// <summary>
/// Verifies that <see cref="OpenAIEmbeddingRequestTranslator"/> produces the
/// exact OpenAI-compatible embeddings request body expected for
/// representative provider-neutral requests, comparing against JSON fixture
/// files rather than JSON literals embedded in test source.
/// </summary>
public sealed class OpenAIEmbeddingRequestTranslatorTests
{
    private static readonly OpenAICompatibilityProfile Profile = new(
        new Uri("https://api.openai.com/"),
        "v1/chat/completions",
        sendDeveloperRoleAsSystem: false,
        preferStreaming: false,
        includeStreamUsage: true,
        useMaxCompletionTokensField: true,
        [],
        embeddingsPath: "v1/embeddings");

    private static readonly OpenAICompatibilityProfile ProfileWithPurposeSupport = new(
        Profile.BaseAddress,
        Profile.ChatCompletionsPath,
        Profile.SendDeveloperRoleAsSystem,
        Profile.PreferStreaming,
        Profile.IncludeStreamUsage,
        Profile.UseMaxCompletionTokensField,
        Profile.DefaultRequestHeaders,
        Profile.EmbeddingsPath,
        supportsEmbeddingPurpose: true);

    private static EmbeddingModelRequest CreateRequest(EmbeddingRequest embeddingRequest) =>
        new(
            new EmbeddingRequestContext(new EmbeddingRequestId(Guid.NewGuid()), TestModels.TextEmbedding3Small, embeddingRequest),
            attempt: 1,
            DateTimeOffset.UtcNow.AddMinutes(1),
            ProviderRequestOptions.Empty);

    [Fact]
    public void Translate_WhenSingleInputWithDimensions_MatchesExpectedRequestBody()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hello world", null)],
            EmbeddingPurpose.Unspecified,
            dimensions: 512,
            encoding: EmbeddingEncoding.Float,
            EmbeddingTruncation.ProviderDefault,
            ExtensionData.Empty);

        var actual = new OpenAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest), Profile);
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

        var actual = new OpenAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest), Profile);
        var expected = JsonNode.Parse(TestResources.ReadAllText("requests/embedding_request_batch.json"));

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Translate_WhenPurposeSpecified_ThrowsNotSupportedException()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)],
            EmbeddingPurpose.Document,
            null,
            null,
            EmbeddingTruncation.ProviderDefault,
            ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new OpenAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest), Profile));
    }

    [Theory]
    [InlineData(EmbeddingPurpose.Query, "search_query")]
    [InlineData(EmbeddingPurpose.Document, "search_document")]
    [InlineData(EmbeddingPurpose.Classification, "classification")]
    [InlineData(EmbeddingPurpose.Clustering, "clustering")]
    public void Translate_WhenProfileSupportsPurposeAndPurposeIsMapped_SerializesInputType(EmbeddingPurpose purpose, string expected)
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)],
            purpose,
            null,
            null,
            EmbeddingTruncation.ProviderDefault,
            ExtensionData.Empty);

        var body = new OpenAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest), ProfileWithPurposeSupport);

        body["input_type"]!.GetValue<string>().ShouldBe(expected);
    }

    [Fact]
    public void Translate_WhenProfileDoesNotSupportPurpose_ThrowsNotSupportedException()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)],
            EmbeddingPurpose.Document,
            null,
            null,
            EmbeddingTruncation.ProviderDefault,
            ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new OpenAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest), Profile));
    }

    [Fact]
    public void Translate_WhenProfileSupportsPurposeButUnspecified_OmitsInputType()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)],
            EmbeddingPurpose.Unspecified,
            null,
            null,
            EmbeddingTruncation.ProviderDefault,
            ExtensionData.Empty);

        var body = new OpenAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest), ProfileWithPurposeSupport);

        body.AsObject().ContainsKey("input_type").ShouldBeFalse();
    }

    [Theory]
    [InlineData(EmbeddingPurpose.Similarity)]
    [InlineData(EmbeddingPurpose.QuestionAnswering)]
    [InlineData(EmbeddingPurpose.CodeRetrieval)]
    public void Translate_WhenProfileSupportsPurposeButPurposeIsUnmapped_ThrowsNotSupportedException(EmbeddingPurpose purpose)
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)],
            purpose,
            null,
            null,
            EmbeddingTruncation.ProviderDefault,
            ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new OpenAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest), ProfileWithPurposeSupport));
    }

    [Fact]
    public void Translate_WhenTruncationSpecified_ThrowsNotSupportedException()
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)],
            EmbeddingPurpose.Unspecified,
            null,
            null,
            EmbeddingTruncation.Reject,
            ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new OpenAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest), Profile));
    }

    [Theory]
    [InlineData(EmbeddingEncoding.Int8)]
    [InlineData(EmbeddingEncoding.UInt8)]
    [InlineData(EmbeddingEncoding.Binary)]
    [InlineData(EmbeddingEncoding.UBinary)]
    public void Translate_WhenUnsupportedEncoding_ThrowsNotSupportedException(EmbeddingEncoding encoding)
    {
        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)],
            EmbeddingPurpose.Unspecified,
            null,
            encoding,
            EmbeddingTruncation.ProviderDefault,
            ExtensionData.Empty);

        _ = Should.Throw<NotSupportedException>(
            () => new OpenAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest), Profile));
    }

    [Fact]
    public void Translate_WhenExtensionsAttemptToOverrideProtectedField_IgnoresOverride()
    {
        var hackedModel = new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes("hacked-model")]);
        var extensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("model", hackedModel));

        var embeddingRequest = new EmbeddingRequest(
            [new TextEmbeddingInput("hi", null)],
            EmbeddingPurpose.Unspecified,
            null,
            null,
            EmbeddingTruncation.ProviderDefault,
            extensions);

        var body = new OpenAIEmbeddingRequestTranslator().Translate(CreateRequest(embeddingRequest), Profile);

        body["model"]!.GetValue<string>().ShouldBe("text-embedding-3-small");
    }
}
