// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

using AgentKit.Providers.GoogleVertexAI.Wire;

/// <summary>
/// The default <see cref="IGoogleVertexAIEmbeddingResponseParser"/>,
/// parsing a buffered Vertex AI <c>:predict</c> response body for a
/// text-embedding model.
/// </summary>
public sealed class GoogleVertexAIEmbeddingResponseParser: IGoogleVertexAIEmbeddingResponseParser
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public async Task<EmbeddingAttemptResult> ParseAsync(
        Stream responseBody,
        GoogleVertexAIEmbeddingResponseParseContext context,
        ImmutableArray<EmbeddingInput> requestInputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseBody);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfDefault(requestInputs);

        GoogleVertexAIPredictResponseDto dto;
        try
        {
            dto = await JsonSerializer
                .DeserializeAsync<GoogleVertexAIPredictResponseDto>(responseBody, _serializerOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new JsonException("The response body deserialized to a null value.");
        }
        catch (JsonException exception)
        {
            return new EmbeddingAttemptFailed(BuildFailure(
                context, "The provider returned a response body that could not be parsed.", exception));
        }

        var predictions = dto.Predictions ?? [];
        if (predictions.Count == 0)
        {
            return new EmbeddingAttemptFailed(BuildFailure(
                context, "The provider returned a predict response with no predictions.", diagnosticCause: null));
        }

        if (predictions.Count != requestInputs.Length)
        {
            return new EmbeddingAttemptFailed(BuildFailure(
                context,
                $"The provider returned {predictions.Count} predictions for a request with " +
                $"{requestInputs.Length} inputs; Vertex AI's predict response carries no explicit index, " +
                "so a mismatched count cannot be safely correlated by position.",
                diagnosticCause: null));
        }

        var identity = context.CreateResponseIdentity();
        var items = ImmutableArray.CreateBuilder<EmbeddingItemOutcome>(predictions.Count);
        long? totalTokenCount = null;

        for (var index = 0; index < predictions.Count; index++)
        {
            var embedding = predictions[index].Embeddings;
            var values = embedding?.Values;
            if (values is not { Count: > 0 })
            {
                return new EmbeddingAttemptFailed(BuildFailure(
                    context, $"The provider returned an empty embedding vector at position {index}.", diagnosticCause: null));
            }

            var vector = new DenseFloatVector([.. values]);
            var correlationId = (requestInputs[index] as TextEmbeddingInput)?.CorrelationId;
            var space = new EmbeddingSpaceIdentity(
                identity,
                vector.Values.Length,
                EmbeddingElementType.Float32,
                context.RequestedPurpose,
                ExtensionData.Empty);

            var extensions = ExtensionData.Empty;
            if (embedding?.Statistics is { } statistics)
            {
                extensions = new ExtensionData(
                    ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                        "truncated",
                        new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(statistics.Truncated)])));

                if (statistics.TokenCount is { } tokenCount)
                {
                    try
                    {
                        ArgumentOutOfRangeException.ThrowIfNegative(tokenCount);
                        totalTokenCount = checked((totalTokenCount ?? 0) + tokenCount);
                    }
                    catch (Exception exception) when (exception is ArgumentException or OverflowException)
                    {
                        return new EmbeddingAttemptFailed(BuildFailure(
                            context,
                            "The provider returned invalid usage evidence.",
                            exception));
                    }
                }
            }

            items.Add(new EmbeddingItemSucceeded(index, correlationId, vector, space, extensions));
        }

        var usage = totalTokenCount is { } total
            ? new ModelUsage(ModelUsageReportState.Final, total, outputTokens: null, cachedInputTokens: null, reasoningTokens: null, estimatedCost: null, costCurrency: null, ExtensionData.Empty)
            : ModelUsage.NotReported;

        var response = new EmbeddingResponse(items.ToImmutable(), usage, context.ProviderRequestId, ExtensionData.Empty);

        return new EmbeddingAttemptCompleted(response);
    }

    private static ProviderFailure BuildFailure(
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
