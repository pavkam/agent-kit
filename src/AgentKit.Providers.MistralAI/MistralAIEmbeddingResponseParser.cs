// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

using System.Diagnostics;

using AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The default <see cref="IMistralAIEmbeddingResponseParser"/>, parsing a
/// buffered Mistral AI embeddings response body.
/// </summary>
/// <remarks>
/// <para>
/// Mistral's embeddings contract returns exactly one <c>data</c> item per
/// request input, each carrying the zero-based <c>index</c> of the input it
/// answers. This parser reconciles the body against the request before it
/// reports success: the number of items must equal the number of inputs,
/// every <c>index</c> must lie within <c>[0, inputs.Length)</c>, and no
/// index may repeat. Any violation, including a batch that omits one or
/// more inputs, yields an <see cref="EmbeddingAttemptFailed"/> with
/// <see cref="ProviderFailureKind.ProtocolViolation"/> rather than a
/// partial <see cref="EmbeddingAttemptCompleted"/>, because the wire
/// contract has no notion of a per-item failure and a missing item cannot
/// be attributed to the provider having rejected that input. Items are
/// emitted in input order regardless of the order in which they arrived.
/// </para>
/// <para>
/// The provider request identifier captured in the parse context, when
/// present, is retained on the response identity, on the
/// <see cref="EmbeddingResponse"/>, and on every protocol failure.
/// </para>
/// </remarks>
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

        if (data.Count != requestInputs.Length)
        {
            return new EmbeddingAttemptFailed(BuildFailure(
                context,
                $"The provider returned {data.Count} embedding item(s) for a request with {requestInputs.Length} " +
                "input(s); every input must receive exactly one item.",
                diagnosticCause: null));
        }

        var identity = context.CreateResponseIdentity(dto.Model);
        var elementType = ElementTypeOf(context.RequestedEncoding);
        var items = new EmbeddingItemOutcome?[requestInputs.Length];

        foreach (var entry in data)
        {
            if (entry.Index < 0 || entry.Index >= requestInputs.Length)
            {
                return new EmbeddingAttemptFailed(BuildFailure(
                    context,
                    $"The provider returned an embedding item with index {entry.Index}, which is outside the " +
                    $"request's input range [0, {requestInputs.Length}).",
                    diagnosticCause: null));
            }

            if (items[entry.Index] is not null)
            {
                return new EmbeddingAttemptFailed(BuildFailure(
                    context,
                    $"The provider returned more than one embedding item with index {entry.Index}.",
                    diagnosticCause: null));
            }

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

            var correlationId = (requestInputs[entry.Index] as TextEmbeddingInput)?.CorrelationId;
            var space = new EmbeddingSpaceIdentity(identity, DimensionsOf(vector), elementType, EmbeddingPurpose.Unspecified, ExtensionData.Empty);

            items[entry.Index] = new EmbeddingItemSucceeded(entry.Index, correlationId, vector, space, ExtensionData.Empty);
        }

        ModelUsage usage;
        try
        {
            usage = BuildUsage(dto.Usage);
        }
        catch (ArgumentException exception)
        {
            return new EmbeddingAttemptFailed(BuildFailure(
                context,
                "The provider returned invalid usage evidence.",
                exception));
        }
        var response = new EmbeddingResponse(ToInputOrder(items), usage, context.ProviderRequestId, ExtensionData.Empty);

        return new EmbeddingAttemptCompleted(response);
    }

    /// <summary>
    /// Materializes the per-input slots, which the count, range, and
    /// uniqueness checks in <see cref="ParseAsync"/> have already filled
    /// completely, as an immutable array in input order.
    /// </summary>
    /// <param name="slots">One slot per request input, each holding exactly one outcome.</param>
    /// <returns>The outcomes in input order.</returns>
    private static ImmutableArray<EmbeddingItemOutcome> ToInputOrder(EmbeddingItemOutcome?[] slots)
    {
        Debug.Assert(Array.TrueForAll(slots, static slot => slot is not null), "Every input slot must hold exactly one outcome.");

        var outcomes = ImmutableArray.CreateBuilder<EmbeddingItemOutcome>(slots.Length);
        foreach (var slot in slots)
        {
            outcomes.Add(slot ?? throw new UnreachableException("An input slot was left unfilled after reconciliation."));
        }

        return outcomes.MoveToImmutable();
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
            ? ModelUsage.NotReported
            : new ModelUsage(ModelUsageReportState.Final,
                usage.PromptTokens,
                outputTokens: null,
                cachedInputTokens: null,
                reasoningTokens: null,
                estimatedCost: null,
                costCurrency: null,
                ExtensionData.Empty);

    private static ProviderFailure BuildFailure(
        MistralAIEmbeddingResponseParseContext context, string safeMessage, Exception? diagnosticCause) =>
        new(
            ProviderFailureKind.ProtocolViolation,
            context.ProviderId,
            context.ProviderRequestId,
            statusCode: null,
            providerCode: null,
            retryAfter: null,
            safeMessage,
            diagnosticCause,
            ExtensionData.Empty);
}
