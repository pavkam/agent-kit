// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

using System.Diagnostics;

/// <summary>
/// The default <see cref="IGoogleVertexAIEmbeddingRequestTranslator"/>,
/// covering the text input, task type, dimensionality, and truncation
/// options supported by Vertex AI's generic <c>:predict</c> wire format for
/// text-embedding models.
/// </summary>
/// <remarks>
/// This is a materially different wire shape from the Gemini Developer
/// API's <c>batchEmbedContents</c> operation reused for conversational
/// generation elsewhere in this package family: <c>:predict</c> uses a
/// generic <c>instances</c>/<c>parameters</c> envelope shared across every
/// Vertex model family, not a <c>content.parts[]</c> structure.
/// </remarks>
public sealed class GoogleVertexAIEmbeddingRequestTranslator: IGoogleVertexAIEmbeddingRequestTranslator
{
    /// <inheritdoc/>
    public JsonObject Translate(EmbeddingModelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var embeddingRequest = request.Context.Request;

        if (embeddingRequest.Encoding is { } encoding && encoding != EmbeddingEncoding.Float)
        {
            throw new NotSupportedException(
                $"Embedding encoding '{encoding}' is not supported by the Vertex AI text-embedding API, " +
                "which always returns floating-point vectors.");
        }

        var instances = new JsonArray();
        foreach (var input in embeddingRequest.Inputs)
        {
            instances.Add(TranslateInstance(input, embeddingRequest.Purpose));
        }

        var body = new JsonObject { ["instances"] = instances };

        var parameters = new JsonObject();
        if (embeddingRequest.Dimensions is { } dimensions)
        {
            parameters["outputDimensionality"] = dimensions;
        }

        switch (embeddingRequest.Truncation)
        {
            case EmbeddingTruncation.ProviderDefault:
                break;

            case EmbeddingTruncation.Reject:
                parameters["autoTruncate"] = false;
                break;

            case EmbeddingTruncation.Start or EmbeddingTruncation.End:
                throw new NotSupportedException(
                    $"Truncation policy '{embeddingRequest.Truncation}' is not supported by the Vertex AI " +
                    "text-embedding API, which only supports enabling or disabling automatic truncation, " +
                    "not choosing its direction.");

            default:
                throw new NotSupportedException(
                    $"Truncation policy '{embeddingRequest.Truncation}' is not supported by the Vertex AI " +
                    "embeddings request translator.");
        }

        if (parameters.Count > 0)
        {
            body["parameters"] = parameters;
        }

        ProviderJson.ApplyExtensions(body, embeddingRequest.Extensions);
        ProviderJson.ApplyExtensions(body, request.Options.Extensions);

        return body;
    }

    private static JsonObject TranslateInstance(EmbeddingInput input, EmbeddingPurpose purpose)
    {
        if (input is not TextEmbeddingInput text)
        {
            throw new NotSupportedException(
                $"Input kind '{input.GetType().Name}' is not supported by the Vertex AI embeddings request " +
                "translator.");
        }

        var instance = new JsonObject { ["content"] = text.Text };

        if (purpose != EmbeddingPurpose.Unspecified)
        {
            instance["task_type"] = TranslatePurpose(purpose);
        }

        return instance;
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
                $"Embedding purpose '{purpose}' is not supported by the Vertex AI embeddings request translator."),
        };

}
