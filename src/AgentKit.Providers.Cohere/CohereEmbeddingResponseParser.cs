// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

using System.Diagnostics;

using AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The default <see cref="ICohereEmbeddingResponseParser"/>, parsing a
/// buffered Cohere v2 embed response body.
/// </summary>
/// <remarks>
/// Cohere's <c>embeddings</c> field is an object keyed by encoding name
/// rather than an array of per-item results, and neither that map nor its
/// per-encoding arrays carry an explicit per-item index. This parser reads
/// only the single key matching the request's requested encoding and maps
/// vectors onto the request inputs purely by array position, failing
/// closed with <see cref="ProviderFailureKind.ProtocolViolation"/> if the
/// counts disagree.
/// </remarks>
public sealed class CohereEmbeddingResponseParser: ICohereEmbeddingResponseParser
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public async Task<EmbeddingAttemptResult> ParseAsync(
        Stream responseBody,
        CohereEmbeddingResponseParseContext context,
        ImmutableArray<EmbeddingInput> requestInputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseBody);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfDefault(requestInputs);

        CohereEmbedResponseDto dto;
        try
        {
            dto = await JsonSerializer
                .DeserializeAsync<CohereEmbedResponseDto>(responseBody, _serializerOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new JsonException("The response body deserialized to a null value.");
        }
        catch (JsonException exception)
        {
            return new EmbeddingAttemptFailed(BuildFailure(
                context, "The provider returned a response body that could not be parsed.", exception));
        }

        var encodingKey = EncodingKeyOf(context.RequestedEncoding);

        if (dto.Embeddings is null || !dto.Embeddings.TryGetValue(encodingKey, out var encodedVectors))
        {
            return new EmbeddingAttemptFailed(BuildFailure(
                context,
                $"The provider response did not include an '{encodingKey}' entry in 'embeddings'.",
                diagnosticCause: null));
        }

        if (encodedVectors.ValueKind != JsonValueKind.Array)
        {
            return new EmbeddingAttemptFailed(BuildFailure(
                context, $"The provider response's 'embeddings.{encodingKey}' field was not a JSON array.", diagnosticCause: null));
        }

        var vectorElements = encodedVectors.EnumerateArray().ToArray();

        if (vectorElements.Length != requestInputs.Length)
        {
            return new EmbeddingAttemptFailed(BuildFailure(
                context,
                $"The provider returned {vectorElements.Length} embedding(s) for {requestInputs.Length} " +
                "requested input(s).",
                diagnosticCause: null));
        }

        var identity = BuildIdentity(context);
        var elementType = ElementTypeOf(context.RequestedEncoding);
        var items = ImmutableArray.CreateBuilder<EmbeddingItemOutcome>(vectorElements.Length);

        for (var index = 0; index < vectorElements.Length; index++)
        {
            EmbeddingVector vector;
            try
            {
                vector = DecodeVector(vectorElements[index], context.RequestedEncoding);
            }
            catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
            {
                return new EmbeddingAttemptFailed(BuildFailure(
                    context, "The provider returned an embedding vector that could not be decoded.", exception));
            }

            var correlationId = (requestInputs[index] as TextEmbeddingInput)?.CorrelationId;
            var space = new EmbeddingSpaceIdentity(identity, DimensionsOf(vector), elementType, context.RequestedPurpose, ExtensionData.Empty);

            items.Add(new EmbeddingItemSucceeded(index, correlationId, vector, space, ExtensionData.Empty));
        }

        ModelUsage usage;
        try
        {
            usage = BuildUsage(dto.Meta);
        }
        catch (ArgumentException exception)
        {
            return new EmbeddingAttemptFailed(BuildFailure(
                context,
                "The provider returned invalid usage evidence.",
                exception));
        }
        var response = new EmbeddingResponse(items.ToImmutable(), usage, providerRequestId: null, ExtensionData.Empty);

        return new EmbeddingAttemptCompleted(response);
    }

    private static string EncodingKeyOf(EmbeddingEncoding encoding) =>
        encoding switch
        {
            EmbeddingEncoding.Float => "float",
            EmbeddingEncoding.Int8 => "int8",
            EmbeddingEncoding.UInt8 => "uint8",
            EmbeddingEncoding.Binary => "binary",
            EmbeddingEncoding.UBinary => "ubinary",
            _ => throw new UnreachableException($"Unrecognized {nameof(EmbeddingEncoding)} value '{encoding}'."),
        };

    private static EmbeddingVector DecodeVector(JsonElement embedding, EmbeddingEncoding encoding) =>
        encoding switch
        {
            EmbeddingEncoding.Float => new DenseFloatVector([.. EnumerateAs(embedding, static element => element.GetSingle())]),
            EmbeddingEncoding.Int8 => new QuantizedByteVector(
                [.. EnumerateAs(embedding, static element => unchecked((byte) element.GetSByte()))], signed: true),
            EmbeddingEncoding.UInt8 => new QuantizedByteVector(
                [.. EnumerateAs(embedding, static element => element.GetByte())], signed: false),
            EmbeddingEncoding.Binary => new PackedBinaryVector(
                [.. EnumerateAs(embedding, static element => unchecked((byte) element.GetSByte()))], signed: true),
            EmbeddingEncoding.UBinary => new PackedBinaryVector(
                [.. EnumerateAs(embedding, static element => element.GetByte())], signed: false),
            _ => throw new UnreachableException($"Unrecognized {nameof(EmbeddingEncoding)} value '{encoding}'."),
        };

    private static IEnumerable<T> EnumerateAs<T>(JsonElement embedding, Func<JsonElement, T> project)
    {
        if (embedding.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("An embedding vector entry was not a JSON array.");
        }

        foreach (var element in embedding.EnumerateArray())
        {
            yield return project(element);
        }
    }

    private static EmbeddingElementType ElementTypeOf(EmbeddingEncoding encoding) =>
        encoding switch
        {
            EmbeddingEncoding.Float => EmbeddingElementType.Float32,
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

    /// <summary>
    /// Builds final usage evidence from one complete Cohere embedding response.
    /// </summary>
    /// <param name="meta">The optional wire metadata.</param>
    /// <returns>Final usage for a supplied billed-unit count, or <see cref="ModelUsage.NotReported"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A supplied wire token count is negative.</exception>
    private static ModelUsage BuildUsage(CohereEmbedMetaDto? meta) =>
        meta?.BilledUnits?.InputTokens is { } inputTokens
            ? new ModelUsage(ModelUsageReportState.Final,
                ToTokenCount(inputTokens),
                outputTokens: null,
                cachedInputTokens: null,
                reasoningTokens: null,
                estimatedCost: null,
                costCurrency: null,
                ExtensionData.Empty)
            : ModelUsage.NotReported;

    /// <summary>
    /// Validates a raw Cohere token count before its portable integer projection.
    /// </summary>
    /// <param name="value">The non-null raw wire count.</param>
    /// <returns>The portable count after validation.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    private static long ToTokenCount(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        return (long) value;
    }

    private static ProviderResponseIdentity BuildIdentity(CohereEmbeddingResponseParseContext context) =>
        new(
            context.ProviderId,
            upstreamProviderId: null,
            context.ApiFamily,
            context.RequestedModelId,
            context.RequestedModelId,
            deploymentId: null,
            requestId: null,
            responseId: null);

    private static ProviderFailure BuildFailure(
        CohereEmbeddingResponseParseContext context, string safeMessage, Exception? diagnosticCause) =>
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
