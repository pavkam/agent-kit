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
public sealed class OpenAIEmbeddingResponseParser: IOpenAIEmbeddingResponseParser
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public async Task<EmbeddingAttemptResult> ParseAsync(
        Stream responseBody,
        OpenAIEmbeddingResponseParseContext context,
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

        var items = ImmutableArray.CreateBuilder<EmbeddingItemOutcome>(data.Count);
        var identity = BuildIdentity(context, dto.Model);

        foreach (var entry in data)
        {
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

            var correlationId = entry.Index >= 0 && entry.Index < requestInputs.Length
                ? (requestInputs[entry.Index] as TextEmbeddingInput)?.CorrelationId
                : null;

            var space = new EmbeddingSpaceIdentity(
                identity,
                DimensionsOf(vector),
                EmbeddingElementType.Float32,
                EmbeddingPurpose.Unspecified,
                ExtensionData.Empty);

            items.Add(new EmbeddingItemSucceeded(entry.Index, correlationId, vector, space, ExtensionData.Empty));
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
        var response = new EmbeddingResponse(items.ToImmutable(), usage, context.ProviderRequestId, ExtensionData.Empty);

        return new EmbeddingAttemptCompleted(response);
    }

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
            var values = ImmutableArray.CreateBuilder<float>(bytes.Length / 4);
            for (var offset = 0; offset + 4 <= bytes.Length; offset += 4)
            {
                values.Add(BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset, 4)));
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

    private static ProviderResponseIdentity BuildIdentity(OpenAIEmbeddingResponseParseContext context, string? resolvedModel) =>
        new(
            context.ProviderId,
            upstreamProviderId: null,
            context.ApiFamily,
            context.RequestedModelId,
            resolvedModel is { Length: > 0 } model ? new ModelId(model) : context.RequestedModelId,
            context.DeploymentId,
            requestId: null,
            responseId: null);

    private static ProviderFailure BuildProtocolFailure(
        OpenAIEmbeddingResponseParseContext context, string safeMessage, Exception? diagnosticCause) =>
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
