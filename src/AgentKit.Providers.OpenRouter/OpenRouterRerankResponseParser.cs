// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter;

using AgentKit.Providers.OpenRouter.Wire;

/// <summary>The default <see cref="IOpenRouterRerankResponseParser"/> for OpenRouter rerank.</summary>
public sealed class OpenRouterRerankResponseParser: IOpenRouterRerankResponseParser
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public async Task<RerankModelResult> ParseAsync(
        Stream responseBody,
        ImmutableArray<RerankDocument> documents,
        ProviderId providerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseBody);
        ArgumentException.ThrowIfDefault(documents);

        OpenRouterRerankResponseDto dto;
        try
        {
            dto = await JsonSerializer
                .DeserializeAsync<OpenRouterRerankResponseDto>(responseBody, _serializerOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new JsonException("The response body deserialized to a null value.");
        }
        catch (JsonException exception)
        {
            return Failed(providerId, ProviderFailureKind.ProtocolViolation, "The provider returned a response body that could not be parsed.", exception);
        }

        var entries = dto.Results;
        if (entries is null || entries.Count == 0)
        {
            return Failed(providerId, ProviderFailureKind.ProtocolViolation, "The provider returned a rerank response with no results.", diagnosticCause: null);
        }

        var results = ImmutableArray.CreateBuilder<RerankResult>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Index < 0 || entry.Index >= documents.Length)
            {
                return Failed(
                    providerId,
                    ProviderFailureKind.ProtocolViolation,
                    $"The provider returned a rerank result with index {entry.Index}, which is outside the request's document range.",
                    diagnosticCause: null);
            }

            var document = documents[entry.Index];
            results.Add(new RerankResult(entry.Index, document.Id, entry.RelevanceScore, ExtensionData.Empty));
        }

        var usage = SemanticOperationUsage.NotReported;
        return new RerankModelSucceeded(new RerankResponse(results.ToImmutable(), usage, providerRequestId: null, ExtensionData.Empty));
    }

    private static RerankModelFailed Failed(
        ProviderId providerId,
        ProviderFailureKind kind,
        string safeMessage,
        Exception? diagnosticCause) =>
        new(
            new ProviderFailure(
                kind,
                providerId,
                requestId: null,
                statusCode: null,
                providerCode: null,
                retryAfter: null,
                safeMessage,
                diagnosticCause,
                ExtensionData.Empty));
}
