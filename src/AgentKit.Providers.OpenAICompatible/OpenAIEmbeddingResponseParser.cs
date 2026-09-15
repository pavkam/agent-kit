// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using System.Buffers.Binary;
using System.Diagnostics;

using AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The default <see cref="IOpenAIEmbeddingResponseParser"/>, parsing a
/// buffered OpenAI-compatible embeddings response body.
/// </summary>
/// <remarks>
/// <para>
/// The OpenAI embeddings contract returns exactly one <c>data</c> item per
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
/// A base64-encoded <c>embedding</c> is a little-endian sequence of IEEE
/// 754 single-precision values; a payload that is not valid base64 or whose
/// decoded length is not a multiple of four bytes is rejected as a protocol
/// violation rather than truncated.
/// </para>
/// <para>
/// The provider request identifier captured in the parse context, when
/// present, is retained on the response identity, on the
/// <see cref="EmbeddingResponse"/>, and on every protocol failure.
/// </para>
/// </remarks>
public sealed class OpenAIEmbeddingResponseParser: IOpenAIEmbeddingResponseParser
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public async Task<EmbeddingAttemptResult> ParseAsync(
        Stream responseBody,
        EmbeddingResponseParseContext context,
        ImmutableArray<EmbeddingInput> requestInputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseBody);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfDefault(requestInputs);

        OpenAIEmbeddingResponseDto dto;
        try
        {
            dto = await JsonSerializer
                .DeserializeAsync<OpenAIEmbeddingResponseDto>(responseBody, _serializerOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new JsonException("The response body deserialized to a null value.");
        }
        catch (JsonException exception)
        {
            return new EmbeddingAttemptFailed(BuildProtocolFailure(
                context, "The provider returned a response body that could not be parsed.", exception));
        }

        var data = dto.Data ?? [];
        if (data.Count == 0)
        {
            return new EmbeddingAttemptFailed(BuildProtocolFailure(
                context, "The provider returned an embeddings response with no data items.", diagnosticCause: null));
        }

        if (data.Count != requestInputs.Length)
        {
            return new EmbeddingAttemptFailed(BuildProtocolFailure(
                context,
                $"The provider returned {data.Count} embedding item(s) for a request with {requestInputs.Length} " +
                "input(s); every input must receive exactly one item.",
                diagnosticCause: null));
        }

        var items = new EmbeddingItemOutcome?[requestInputs.Length];
        var identity = context.CreateResponseIdentity(dto.Model);

        foreach (var entry in data)
        {
            if (entry.Index < 0 || entry.Index >= requestInputs.Length)
            {
                return new EmbeddingAttemptFailed(BuildProtocolFailure(
                    context,
                    $"The provider returned an embedding item with index {entry.Index}, which is outside the " +
                    $"request's input range [0, {requestInputs.Length}).",
                    diagnosticCause: null));
            }

            if (items[entry.Index] is not null)
            {
                return new EmbeddingAttemptFailed(BuildProtocolFailure(
                    context,
                    $"The provider returned more than one embedding item with index {entry.Index}.",
                    diagnosticCause: null));
            }

            EmbeddingVector vector;
            try
            {
                vector = DecodeVector(entry.Embedding);
            }
            catch (Exception exception) when (exception is JsonException or FormatException)
            {
                return new EmbeddingAttemptFailed(BuildProtocolFailure(
                    context, "The provider returned an embedding vector that could not be decoded.", exception));
            }

            var correlationId = (requestInputs[entry.Index] as TextEmbeddingInput)?.CorrelationId;
            var space = new EmbeddingSpaceIdentity(
                identity,
                DimensionsOf(vector),
                EmbeddingElementType.Float32,
                EmbeddingPurpose.Unspecified,
                ExtensionData.Empty);

            items[entry.Index] = new EmbeddingItemSucceeded(entry.Index, correlationId, vector, space, ExtensionData.Empty);
        }

        ModelUsage usage;
        try
        {
            usage = BuildUsage(dto.Usage);
        }
        catch (ArgumentException exception)
        {
            return new EmbeddingAttemptFailed(BuildProtocolFailure(
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

    /// <summary>
    /// Decodes one <c>embedding</c> field into a dense float vector.
    /// </summary>
    /// <param name="embedding">The raw <c>embedding</c> JSON value: a number array or a base64 string.</param>
    /// <returns>The decoded vector.</returns>
    /// <exception cref="JsonException">
    /// The value is neither an array nor a string, or an array element is not a number.
    /// </exception>
    /// <exception cref="FormatException">
    /// The string is not valid base64, or its decoded length is not a whole
    /// number of little-endian single-precision values.
    /// </exception>
    private static DenseFloatVector DecodeVector(JsonElement embedding)
    {
        if (embedding.ValueKind == JsonValueKind.Array)
        {
            var values = ImmutableArray.CreateBuilder<float>(embedding.GetArrayLength());
            foreach (var element in embedding.EnumerateArray())
            {
                values.Add(element.GetSingle());
            }

            return new DenseFloatVector(values.ToImmutable());
        }

        if (embedding.ValueKind == JsonValueKind.String)
        {
            var bytes = Convert.FromBase64String(embedding.GetString() ?? string.Empty);
            if (bytes.Length % sizeof(float) != 0)
            {
                throw new FormatException(
                    $"The base64 embedding payload decoded to {bytes.Length} byte(s), which is not a whole number " +
                    $"of {sizeof(float)}-byte single-precision values.");
            }

            var values = ImmutableArray.CreateBuilder<float>(bytes.Length / sizeof(float));
            for (var offset = 0; offset < bytes.Length; offset += sizeof(float))
            {
                values.Add(BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset, sizeof(float))));
            }

            return new DenseFloatVector(values.ToImmutable());
        }

        throw new JsonException("The 'embedding' field was neither a JSON array nor a base64-encoded string.");
    }

    private static int DimensionsOf(EmbeddingVector vector) =>
        vector switch
        {
            DenseFloatVector dense => dense.Values.Length,
            QuantizedByteVector quantized => quantized.Values.Length,
            PackedBinaryVector packed => packed.Values.Length * 8,
            _ => throw new UnreachableException($"Unrecognized {nameof(EmbeddingVector)} kind '{vector.GetType().Name}'."),
        };

    private static ModelUsage BuildUsage(OpenAIUsage? usage) =>
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

    private static ProviderFailure BuildProtocolFailure(
        EmbeddingResponseParseContext context, string safeMessage, Exception? diagnosticCause) =>
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
