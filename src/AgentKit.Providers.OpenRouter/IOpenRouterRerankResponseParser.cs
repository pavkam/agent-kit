// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenRouter;

/// <summary>Parses OpenRouter rerank responses into normalized rerank results.</summary>
public interface IOpenRouterRerankResponseParser
{
    /// <summary>Parses one buffered OpenRouter rerank response body.</summary>
    /// <param name="responseBody">The response body stream.</param>
    /// <param name="documents">The documents from the original request, in input order.</param>
    /// <param name="providerId">The provider identity for failures.</param>
    /// <param name="cancellationToken">A token used to cancel the read.</param>
    /// <returns>The normalized rerank outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="responseBody"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="documents"/> is default.</exception>
    public Task<RerankModelResult> ParseAsync(
        Stream responseBody,
        ImmutableArray<RerankDocument> documents,
        ProviderId providerId,
        CancellationToken cancellationToken = default);
}
