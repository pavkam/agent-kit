// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

/// <summary>Parses a buffered Cohere v2 rerank HTTP response into a terminal outcome.</summary>
public interface ICohereRerankResponseParser
{
    /// <summary>Parses one rerank response body.</summary>
    /// <param name="responseBody">The response body stream.</param>
    /// <param name="documents">The request documents in submission order.</param>
    /// <param name="providerId">The provider identity for failures.</param>
    /// <param name="cancellationToken">A token used to cancel parsing.</param>
    /// <returns>The terminal rerank attempt outcome.</returns>
    public Task<RerankModelResult> ParseAsync(
        Stream responseBody,
        ImmutableArray<RerankDocument> documents,
        ProviderId providerId,
        CancellationToken cancellationToken = default);
}
