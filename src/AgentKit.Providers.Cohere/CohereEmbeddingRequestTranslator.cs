// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

using System.Diagnostics;

/// <summary>
/// The default <see cref="ICohereEmbeddingRequestTranslator"/>, covering
/// the text input, purpose, output dimensionality, encoding, and
/// truncation options supported by the Cohere v2 <c>POST /v2/embed</c>
/// wire format.
/// </summary>
/// <remarks>
/// Cohere's v2 embed endpoint requires an <c>input_type</c> for every
/// request, with no provider-side default that is safe to assume on the
/// caller's behalf, so a request with <see cref="EmbeddingPurpose.Unspecified"/>
/// is rejected rather than guessed. <c>embedding_types</c> is always sent
/// as a single-element array matching the request's one requested
/// <see cref="EmbeddingEncoding"/>, since this repository's portable
/// contract requests exactly one encoding per call even though Cohere can
/// return several simultaneously.
/// </remarks>
public sealed class CohereEmbeddingRequestTranslator: ICohereEmbeddingRequestTranslator
{
    /// <inheritdoc/>
    public JsonObject Translate(EmbeddingModelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = request.Context;
        var embeddingRequest = context.Request;

        if (embeddingRequest.Purpose == EmbeddingPurpose.Unspecified)
        {
            throw new NotSupportedException(
                "The Cohere v2 embed API requires an 'input_type' for every request; a purpose of " +
                "Unspecified has no safe default and is not supported by this translator.");
        }

        var body = new JsonObject
        {
            ["model"] = context.Model.ModelId.Value,
            ["texts"] = TranslateInputs(embeddingRequest.Inputs),
            ["input_type"] = TranslatePurpose(embeddingRequest.Purpose),
            ["embedding_types"] = new JsonArray(TranslateEncoding(embeddingRequest.Encoding ?? EmbeddingEncoding.Float)),
        };

        if (embeddingRequest.Dimensions is { } dimensions)
        {
            body["output_dimension"] = dimensions;
        }

        if (embeddingRequest.Truncation != EmbeddingTruncation.ProviderDefault)
        {
            body["truncate"] = TranslateTruncation(embeddingRequest.Truncation);
        }

        ProviderJson.ApplyExtensions(body, embeddingRequest.Extensions);
        ProviderJson.ApplyExtensions(body, request.Options.Extensions);

        return body;
    }

    private static JsonArray TranslateInputs(ImmutableArray<EmbeddingInput> inputs)
    {
        var result = new JsonArray();

        foreach (var input in inputs)
        {
            if (input is not TextEmbeddingInput text)
            {
                throw new NotSupportedException(
                    $"Input kind '{input.GetType().Name}' is not supported by the Cohere embeddings request " +
                    "translator.");
            }

            result.Add(JsonValue.Create(text.Text));
        }

        return result;
    }

    private static string TranslatePurpose(EmbeddingPurpose purpose) =>
        purpose switch
        {
            EmbeddingPurpose.Query => "search_query",
            EmbeddingPurpose.Document => "search_document",
            EmbeddingPurpose.Classification => "classification",
            EmbeddingPurpose.Clustering => "clustering",
            EmbeddingPurpose.Unspecified =>
                throw new UnreachableException("The caller must not translate an Unspecified purpose."),
            EmbeddingPurpose.Similarity or EmbeddingPurpose.QuestionAnswering or EmbeddingPurpose.CodeRetrieval =>
                throw new NotSupportedException(
                    $"Embedding purpose '{purpose}' has no known 'input_type' equivalent supported by this " +
                    "translator."),
            _ => throw new NotSupportedException(
                $"Embedding purpose '{purpose}' is not supported by the Cohere embeddings request translator."),
        };

    private static string TranslateEncoding(EmbeddingEncoding encoding) =>
        encoding switch
        {
            EmbeddingEncoding.Float => "float",
            EmbeddingEncoding.Int8 => "int8",
            EmbeddingEncoding.UInt8 => "uint8",
            EmbeddingEncoding.Binary => "binary",
            EmbeddingEncoding.UBinary => "ubinary",
            _ => throw new UnreachableException($"Unrecognized {nameof(EmbeddingEncoding)} value '{encoding}'."),
        };

    private static string TranslateTruncation(EmbeddingTruncation truncation) =>
        truncation switch
        {
            EmbeddingTruncation.Reject => "NONE",
            EmbeddingTruncation.Start => "START",
            EmbeddingTruncation.End => "END",
            EmbeddingTruncation.ProviderDefault =>
                throw new UnreachableException("The caller must not translate a ProviderDefault truncation policy."),
            _ => throw new NotSupportedException(
                $"Truncation policy '{truncation}' is not supported by the Cohere embeddings request translator."),
        };

}
