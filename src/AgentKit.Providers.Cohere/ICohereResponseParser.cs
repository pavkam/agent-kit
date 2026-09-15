// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

/// <summary>
/// Parses a raw Cohere v2 Chat HTTP response body into the ordered
/// <see cref="ModelResponseEvent"/> sequence delivered to an
/// <see cref="IModelResponseObserver"/>, and returns the matching terminal
/// <see cref="ModelAttemptResult"/>.
/// </summary>
/// <remarks>
/// Implementations parse the response as a typed state machine and never
/// emit a successful terminal result after a truncated or malformed
/// response; a broken response produces a <see cref="ModelAttemptFailed"/>
/// with <see cref="ProviderFailureKind.ProtocolViolation"/> instead.
/// </remarks>
public interface ICohereResponseParser
{
    /// <summary>Parses a single, complete, buffered JSON response body.</summary>
    /// <param name="responseBody">The response body stream to read.</param>
    /// <param name="context">The correlation and identity context for this response.</param>
    /// <param name="observer">The observer that receives the ordered response event sequence.</param>
    /// <param name="cancellationToken">A token used to cancel parsing.</param>
    /// <returns>The terminal outcome of the attempt.</returns>
    public Task<ModelAttemptResult> ParseBufferedAsync(
        Stream responseBody,
        ProviderResponseParseContext context,
        IModelResponseObserver observer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses a server-sent-events response body carrying Cohere's
    /// semantic event grammar (<c>message-start</c>,
    /// <c>content-start</c>/<c>content-delta</c>/<c>content-end</c>,
    /// <c>tool-call-start</c>/<c>tool-call-delta</c>/<c>tool-call-end</c>,
    /// and <c>message-end</c>), delivering incremental events as each
    /// arrives.
    /// </summary>
    /// <param name="responseBody">The response body stream to read incrementally.</param>
    /// <param name="context">The correlation and identity context for this response.</param>
    /// <param name="observer">The observer that receives the ordered response event sequence.</param>
    /// <param name="cancellationToken">A token used to cancel parsing.</param>
    /// <returns>The terminal outcome of the attempt.</returns>
    public Task<ModelAttemptResult> ParseStreamingAsync(
        Stream responseBody,
        ProviderResponseParseContext context,
        IModelResponseObserver observer,
        CancellationToken cancellationToken = default);
}
