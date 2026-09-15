// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

using AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The default <see cref="IGoogleGeminiEmbeddingResponseParser"/>, parsing
/// a buffered Gemini <c>batchEmbedContents</c> response body.
/// </summary>
public sealed class GoogleGeminiEmbeddingResponseParser: IGoogleGeminiEmbeddingResponseParser
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

        GoogleGeminiBatchEmbedContentsResponseDto dto;
        try
        {
            dto = await JsonSerializer
                .DeserializeAsync<GoogleGeminiBatchEmbedContentsResponseDto>(responseBody, _serializerOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new JsonException("The response body deserialized to a null value.");
        }
        catch (JsonException exception)
        {
            return new EmbeddingAttemptFailed(BuildFailure(
                context, "The provider returned a response body that could not be parsed.", exception));
        }

        var embeddings = dto.Embeddings ?? [];
        if (embeddings.Count == 0)
        {
            return new EmbeddingAttemptFailed(BuildFailure(
                context, "The provider returned a batchEmbedContents response with no embeddings.", diagnosticCause: null));
        }

        if (embeddings.Count != requestInputs.Length)
        {
            return new EmbeddingAttemptFailed(BuildFailure(
                context,
                $"The provider returned {embeddings.Count} embeddings for a request with " +
                $"{requestInputs.Length} inputs; Gemini's batchEmbedContents response carries no explicit " +
                "index, so a mismatched count cannot be safely correlated by position.",
                diagnosticCause: null));
        }

        var identity = context.CreateResponseIdentity();
        var items = ImmutableArray.CreateBuilder<EmbeddingItemOutcome>(embeddings.Count);

        for (var index = 0; index < embeddings.Count; index++)
        {
            var values = embeddings[index].Values;
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
                EmbeddingPurpose.Unspecified,
                ExtensionData.Empty);

            items.Add(new EmbeddingItemSucceeded(index, correlationId, vector, space, ExtensionData.Empty));
        }

        var response = new EmbeddingResponse(items.ToImmutable(), ModelUsage.NotReported, context.ProviderRequestId, ExtensionData.Empty);

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
