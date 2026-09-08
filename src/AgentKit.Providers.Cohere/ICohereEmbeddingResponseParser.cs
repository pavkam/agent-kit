// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

/// <summary>
/// Parses a raw Cohere v2 embed HTTP response body into a terminal
/// <see cref="EmbeddingAttemptResult"/>.
/// </summary>
public interface ICohereEmbeddingResponseParser
{
    /// <summary>Parses a single, complete, buffered JSON embed response body.</summary>
    /// <param name="responseBody">The response body stream to read.</param>
    /// <param name="context">The correlation and identity context for this response.</param>
    /// <param name="requestInputs">
    /// The originating request's inputs, in order, used to echo each
    /// input's caller-supplied <see cref="EmbeddingInputId"/> correlation
    /// identity back onto its matching result. Cohere's response carries
    /// no explicit per-item index, so results are matched to inputs
    /// purely by array position.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel parsing.</param>
    /// <returns>The terminal outcome of the attempt.</returns>
    public Task<EmbeddingAttemptResult> ParseAsync(
        Stream responseBody,
        CohereEmbeddingResponseParseContext context,
        ImmutableArray<EmbeddingInput> requestInputs,
        CancellationToken cancellationToken = default);
}
