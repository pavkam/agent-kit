// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

/// <summary>
/// The default <see cref="IGoogleGeminiEmbeddingRequestTranslator"/>,
/// covering the text input, task type, dimensionality, and truncation
/// options supported by the Gemini <c>batchEmbedContents</c> wire format.
/// </summary>
/// <remarks>
/// This translator always targets <c>batchEmbedContents</c>, even for a
/// single input, so the adapter has exactly one request/response code path
/// to maintain. Every nested request's <c>model</c> field is set to match
/// the outer path model, as Gemini requires.
/// </remarks>
public sealed class GoogleGeminiEmbeddingRequestTranslator: IGoogleGeminiEmbeddingRequestTranslator
{
    /// <inheritdoc/>
    public JsonObject Translate(EmbeddingModelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = request.Context;
        var embeddingRequest = context.Request;
        var modelResource = $"models/{context.Model.ModelId.Value}";

        var requests = new JsonArray();
        foreach (var input in embeddingRequest.Inputs)
        {
            requests.Add(TranslateOne(input, modelResource, embeddingRequest));
        }

        var body = new JsonObject { ["requests"] = requests };

        ProviderJson.ApplyExtensions(body, embeddingRequest.Extensions);
        ProviderJson.ApplyExtensions(body, request.Options.Extensions);

        return body;
    }

    private static JsonObject TranslateOne(EmbeddingInput input, string modelResource, EmbeddingRequest embeddingRequest)
    {
        if (input is not TextEmbeddingInput text)
        {
            throw new NotSupportedException(
                $"Input kind '{input.GetType().Name}' is not supported by the Gemini embeddings request " +
                "translator.");
        }

        var nested = new JsonObject
        {
            ["model"] = modelResource,
            ["content"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = text.Text }),
            },
        };

        if (embeddingRequest.Purpose != EmbeddingPurpose.Unspecified)
        {
            nested["taskType"] = TranslatePurpose(embeddingRequest.Purpose);
        }

        if (embeddingRequest.Dimensions is { } dimensions)
        {
            nested["outputDimensionality"] = dimensions;
        }

        if (embeddingRequest.Encoding is { } encoding && encoding != EmbeddingEncoding.Float)
        {
            throw new NotSupportedException(
                $"Embedding encoding '{encoding}' is not supported by the Gemini embeddings API, which " +
                "always returns floating-point vectors.");
        }

        switch (embeddingRequest.Truncation)
        {
            case EmbeddingTruncation.ProviderDefault:
                break;

            case EmbeddingTruncation.Reject:
                nested["autoTruncate"] = false;
                break;

            case EmbeddingTruncation.Start or EmbeddingTruncation.End:
                throw new NotSupportedException(
                    $"Truncation policy '{embeddingRequest.Truncation}' is not supported by the Gemini " +
                    "embeddings API, which only supports enabling or disabling automatic truncation, not " +
                    "choosing its direction.");

            default:
                throw new NotSupportedException(
                    $"Truncation policy '{embeddingRequest.Truncation}' is not supported by the Gemini " +
                    "embeddings request translator.");
        }

        return nested;
    }

    private static string TranslatePurpose(EmbeddingPurpose purpose) =>
        purpose switch
        {
            EmbeddingPurpose.Query => "RETRIEVAL_QUERY",
            EmbeddingPurpose.Document => "RETRIEVAL_DOCUMENT",
            EmbeddingPurpose.Similarity => "SEMANTIC_SIMILARITY",
            EmbeddingPurpose.Classification => "CLASSIFICATION",
            EmbeddingPurpose.Clustering => "CLUSTERING",
            EmbeddingPurpose.QuestionAnswering => "QUESTION_ANSWERING",
            EmbeddingPurpose.CodeRetrieval => "CODE_RETRIEVAL_QUERY",
            EmbeddingPurpose.Unspecified => throw new UnreachableException("The caller must not translate an Unspecified purpose."),
            _ => throw new NotSupportedException(
                $"Embedding purpose '{purpose}' is not supported by the Gemini embeddings request translator."),
        };

}
