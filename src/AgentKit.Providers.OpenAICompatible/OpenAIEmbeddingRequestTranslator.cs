// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The default <see cref="IOpenAIEmbeddingRequestTranslator"/>, covering the
/// text input, dimensions, and encoding options supported by the
/// OpenAI-compatible <c>POST /embeddings</c> wire format.
/// </summary>
/// <remarks>
/// The OpenAI-compatible embeddings dialect has no purpose/task-type
/// parameter and no explicit truncation-policy control, unlike Cohere's or
/// Google's embedding APIs; a request that asks for a specific
/// <see cref="EmbeddingPurpose"/> other than <see cref="EmbeddingPurpose.Unspecified"/>
/// or a <see cref="EmbeddingTruncation"/> other than
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

        if (embeddingRequest.Purpose != EmbeddingPurpose.Unspecified)
        {
            throw new NotSupportedException(
                "The OpenAI-compatible embeddings API has no task-type/purpose parameter; a purpose other " +
                "than Unspecified is not supported by this translator.");
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
