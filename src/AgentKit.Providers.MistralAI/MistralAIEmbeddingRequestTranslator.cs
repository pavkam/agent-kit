// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

using System.Diagnostics;

/// <summary>
/// The default <see cref="IMistralAIEmbeddingRequestTranslator"/>, covering
/// the text input, output dimensionality, and output data-type options
/// supported by the Mistral AI <c>POST /v1/embeddings</c> wire format.
/// </summary>
/// <remarks>
/// Mistral's embeddings API has no task-type/purpose parameter and no
/// explicit truncation-policy control; a request that asks for a specific
/// <see cref="EmbeddingPurpose"/> other than <see cref="EmbeddingPurpose.Unspecified"/>
/// or a <see cref="EmbeddingTruncation"/> other than
/// <see cref="EmbeddingTruncation.ProviderDefault"/> is rejected rather
/// than silently ignored, per this repository's semantic-operation adapter
/// rules. Unlike the OpenAI-compatible dialects, Mistral's <c>output_dtype</c>
/// field natively supports quantized and packed-binary vectors, so every
/// <see cref="EmbeddingEncoding"/> value this repository models is
/// supported here.
/// </remarks>
public sealed class MistralAIEmbeddingRequestTranslator: IMistralAIEmbeddingRequestTranslator
{
    /// <inheritdoc/>
    public JsonObject Translate(EmbeddingModelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = request.Context;
        var embeddingRequest = context.Request;

        if (embeddingRequest.Purpose != EmbeddingPurpose.Unspecified)
        {
            throw new NotSupportedException(
                "The Mistral AI embeddings API has no task-type/purpose parameter; a purpose other than " +
                "Unspecified is not supported by this translator.");
        }

        if (embeddingRequest.Truncation != EmbeddingTruncation.ProviderDefault)
        {
            throw new NotSupportedException(
                "The Mistral AI embeddings API has no explicit truncation-policy control; a truncation " +
                "policy other than ProviderDefault is not supported by this translator.");
        }

        var body = new JsonObject
        {
            ["model"] = context.Model.ModelId.Value,
            ["input"] = TranslateInputs(embeddingRequest.Inputs),
        };

        if (embeddingRequest.Dimensions is { } dimensions)
        {
            body["output_dimension"] = dimensions;
        }

        if (embeddingRequest.Encoding is { } encoding)
        {
            body["output_dtype"] = TranslateEncoding(encoding);
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
                    $"Input kind '{input.GetType().Name}' is not supported by the Mistral AI embeddings " +
                    "request translator.");
            }

            result.Add(JsonValue.Create(text.Text));
        }

        return result;
    }

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
