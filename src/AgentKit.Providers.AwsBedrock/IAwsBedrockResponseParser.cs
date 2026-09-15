// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

/// <summary>
/// Parses a raw Bedrock Converse/ConverseStream HTTP response body into the
/// ordered <see cref="ModelResponseEvent"/> sequence delivered to an
/// <see cref="IModelResponseObserver"/>, and returns the matching terminal
/// <see cref="ModelAttemptResult"/>.
/// </summary>
/// <remarks>
/// Implementations parse the response as a typed state machine and never
/// emit a successful terminal result after a truncated or malformed
/// response; a broken response produces a <see cref="ModelAttemptFailed"/>
/// with <see cref="ProviderFailureKind.ProtocolViolation"/> instead.
/// </remarks>
public interface IAwsBedrockResponseParser
{
    /// <summary>Parses a single, complete, buffered JSON <c>Converse</c> response body.</summary>
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
    /// Parses a binary AWS event-stream framed <c>ConverseStream</c>
    /// response body, delivering incremental events as each Converse
    /// streaming event arrives.
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
