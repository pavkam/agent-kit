// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using System.Diagnostics;

/// <summary>
/// The default <see cref="IOpenAIEmbeddingRequestTranslator"/>, covering the
/// text input, dimensions, encoding, and (where the endpoint supports it)
/// purpose options of the OpenAI-compatible <c>POST /embeddings</c> wire
/// format.
/// </summary>
/// <remarks>
/// Plain OpenAI's embeddings dialect has no purpose/task-type parameter,
/// while OpenRouter's compatible dialect accepts one as <c>input_type</c>;
/// <see cref="OpenAICompatibilityProfile.SupportsEmbeddingPurpose"/>
/// selects which behavior this translator applies for a given endpoint. No
/// OpenAI-compatible dialect this repository targets exposes an explicit
/// truncation-policy control; a request that asks for a
/// <see cref="EmbeddingTruncation"/> other than
/// <see cref="EmbeddingTruncation.ProviderDefault"/> is rejected rather than
/// silently ignored, per this repository's semantic-operation adapter
/// rules.
/// </remarks>
public sealed class OpenAIEmbeddingRequestTranslator: IOpenAIEmbeddingRequestTranslator
{
    /// <inheritdoc/>
    public JsonObject Translate(EmbeddingModelRequest request, OpenAICompatibilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);

        var context = request.Context;
        var embeddingRequest = context.Request;

        if (embeddingRequest.Purpose != EmbeddingPurpose.Unspecified && !profile.SupportsEmbeddingPurpose)
        {
            throw new NotSupportedException(
                "This OpenAI-compatible embeddings endpoint has no task-type/purpose parameter; a purpose " +
                "other than Unspecified is not supported by this translator.");
        }

        if (embeddingRequest.Truncation != EmbeddingTruncation.ProviderDefault)
        {
            throw new NotSupportedException(
                "The OpenAI-compatible embeddings API has no explicit truncation-policy control; a " +
                "truncation policy other than ProviderDefault is not supported by this translator.");
        }

        var body = new JsonObject
        {
            ["model"] = context.Model.ModelId.Value,
            ["input"] = TranslateInputs(embeddingRequest.Inputs),
        };

        if (embeddingRequest.Dimensions is { } dimensions)
        {
            body["dimensions"] = dimensions;
        }

        if (embeddingRequest.Encoding is { } encoding)
        {
            body["encoding_format"] = TranslateEncoding(encoding);
        }

        if (embeddingRequest.Purpose != EmbeddingPurpose.Unspecified)
        {
            body["input_type"] = TranslatePurpose(embeddingRequest.Purpose);
        }

        ApplyExtensions(body, embeddingRequest.Extensions);
        ApplyExtensions(body, request.Options.Extensions);

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
                    $"Input kind '{input.GetType().Name}' is not supported by the OpenAI-compatible " +
                    "embeddings request translator.");
            }

            result.Add(JsonValue.Create(text.Text));
        }

        return result;
    }

    private static string TranslateEncoding(EmbeddingEncoding encoding) =>
        encoding switch
        {
            EmbeddingEncoding.Float => "float",
            EmbeddingEncoding.Int8 or EmbeddingEncoding.UInt8 or EmbeddingEncoding.Binary or EmbeddingEncoding.UBinary =>
                throw new NotSupportedException(
                    $"Embedding encoding '{encoding}' is not supported by the OpenAI-compatible embeddings " +
                    "API, which only returns floating-point or base64-encoded floating-point vectors."),
            _ => throw new NotSupportedException(
                $"Embedding encoding '{encoding}' is not supported by the OpenAI-compatible embeddings " +
                "request translator."),
        };

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
                $"Embedding purpose '{purpose}' is not supported by the OpenAI-compatible embeddings " +
                "request translator."),
        };

    private static void ApplyExtensions(JsonObject body, ExtensionData extensions)
    {
        foreach (var (key, value) in extensions.Values)
        {
            // Extension data never overrides a field the translator itself
            // owns; a protected core field cannot be reshaped by a
            // passthrough option.
            if (body.ContainsKey(key))
            {
                continue;
            }

            body[key] = JsonNode.Parse(value.CanonicalJson.AsSpan());
        }
    }
}
