// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

using System.Diagnostics;

using AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The default <see cref="IMistralAIEmbeddingResponseParser"/>, parsing a
/// buffered Mistral AI embeddings response body.
/// </summary>
public sealed class MistralAIEmbeddingResponseParser: IMistralAIEmbeddingResponseParser
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public async Task<EmbeddingAttemptResult> ParseAsync(
        Stream responseBody,
        MistralAIEmbeddingResponseParseContext context,
        ImmutableArray<EmbeddingInput> requestInputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseBody);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfDefault(requestInputs);

        MistralAIEmbeddingResponseDto dto;
        try
        {
            dto = await JsonSerializer
                .DeserializeAsync<MistralAIEmbeddingResponseDto>(responseBody, _serializerOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new JsonException("The response body deserialized to a null value.");
        }
        catch (JsonException exception)
        {
            return new EmbeddingAttemptFailed(BuildFailure(
                context, "The provider returned a response body that could not be parsed.", exception));
        }

        var data = dto.Data ?? [];
        if (data.Count == 0)
        {
            return new EmbeddingAttemptFailed(BuildFailure(
                context, "The provider returned an embeddings response with no data items.", diagnosticCause: null));
        }

        var identity = BuildIdentity(context, dto.Model);
        var elementType = ElementTypeOf(context.RequestedEncoding);
        var items = ImmutableArray.CreateBuilder<EmbeddingItemOutcome>(data.Count);

        foreach (var entry in data)
        {
            EmbeddingVector vector;
            try
            {
                vector = DecodeVector(entry.Embedding, context.RequestedEncoding);
            }
            catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
            {
                return new EmbeddingAttemptFailed(BuildFailure(
                    context, "The provider returned an embedding vector that could not be decoded.", exception));
            }

            var correlationId = entry.Index >= 0 && entry.Index < requestInputs.Length
                ? (requestInputs[entry.Index] as TextEmbeddingInput)?.CorrelationId
                : null;

            var space = new EmbeddingSpaceIdentity(identity, DimensionsOf(vector), elementType, EmbeddingPurpose.Unspecified, ExtensionData.Empty);

            items.Add(new EmbeddingItemSucceeded(entry.Index, correlationId, vector, space, ExtensionData.Empty));
        }

        var usage = BuildUsage(dto.Usage);
        var response = new EmbeddingResponse(items.ToImmutable(), usage, providerRequestId: null, ExtensionData.Empty);

        return new EmbeddingAttemptCompleted(response);
    }

    private static EmbeddingVector DecodeVector(JsonElement embedding, EmbeddingEncoding? requestedEncoding) =>
        requestedEncoding switch
        {
            null or EmbeddingEncoding.Float => new DenseFloatVector([.. EnumerateAs(embedding, static element => element.GetSingle())]),
            EmbeddingEncoding.Int8 => new QuantizedByteVector(
                [.. EnumerateAs(embedding, static element => unchecked((byte) element.GetSByte()))], signed: true),
            EmbeddingEncoding.UInt8 => new QuantizedByteVector(
                [.. EnumerateAs(embedding, static element => element.GetByte())], signed: false),
            EmbeddingEncoding.Binary => new PackedBinaryVector(
                [.. EnumerateAs(embedding, static element => unchecked((byte) element.GetSByte()))], signed: true),
            EmbeddingEncoding.UBinary => new PackedBinaryVector(
                [.. EnumerateAs(embedding, static element => element.GetByte())], signed: false),
            _ => throw new UnreachableException($"Unrecognized {nameof(EmbeddingEncoding)} value '{requestedEncoding}'."),
        };

    private static IEnumerable<T> EnumerateAs<T>(JsonElement embedding, Func<JsonElement, T> project)
    {
        if (embedding.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The 'embedding' field was not a JSON array.");
        }

        foreach (var element in embedding.EnumerateArray())
        {
            yield return project(element);
        }
    }

    private static EmbeddingElementType ElementTypeOf(EmbeddingEncoding? encoding) =>
        encoding switch
        {
            null or EmbeddingEncoding.Float => EmbeddingElementType.Float32,
            EmbeddingEncoding.Int8 => EmbeddingElementType.Int8,
            EmbeddingEncoding.UInt8 => EmbeddingElementType.UInt8,
            EmbeddingEncoding.Binary => EmbeddingElementType.Binary,
            EmbeddingEncoding.UBinary => EmbeddingElementType.UBinary,
            _ => throw new UnreachableException($"Unrecognized {nameof(EmbeddingEncoding)} value '{encoding}'."),
        };

    private static int DimensionsOf(EmbeddingVector vector) =>
        vector switch
        {
            DenseFloatVector dense => dense.Values.Length,
            QuantizedByteVector quantized => quantized.Values.Length,
            PackedBinaryVector packed => packed.Values.Length * 8,
            _ => throw new UnreachableException($"Unrecognized {nameof(EmbeddingVector)} kind '{vector.GetType().Name}'."),
        };

    private static ModelUsage BuildUsage(MistralAIUsageDto? usage) =>
        usage is null
            ? ModelUsage.Empty
            : new ModelUsage(
                usage.PromptTokens,
                outputTokens: null,
                cachedInputTokens: null,
                reasoningTokens: null,
                estimatedCost: null,
                costCurrency: null,
                ExtensionData.Empty);

    private static ProviderResponseIdentity BuildIdentity(MistralAIEmbeddingResponseParseContext context, string? resolvedModel) =>
        new(
            context.ProviderId,
            upstreamProviderId: null,
            context.ApiFamily,
            context.RequestedModelId,
            resolvedModel is { Length: > 0 } model ? new ModelId(model) : context.RequestedModelId,
            deploymentId: null,
            requestId: null,
            responseId: null);

    private static ProviderFailure BuildFailure(
        MistralAIEmbeddingResponseParseContext context, string safeMessage, Exception? diagnosticCause) =>
        new(
            ProviderFailureKind.ProtocolViolation,
            context.ProviderId,
            requestId: null,
            statusCode: null,
            providerCode: null,
            retryAfter: null,
            safeMessage,
            diagnosticCause,
            ExtensionData.Empty);
}
