// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

using System.Net;

/// <summary>
/// Maps a Cohere HTTP error status code onto the normalized
/// <see cref="ProviderFailureKind"/> taxonomy.
/// </summary>
/// <remarks>
/// Cohere's error contract is a flat <c>{"message": "..."}</c> body with no
/// canonical machine-readable error-type vocabulary, so this mapping is
/// HTTP-status-driven. Cohere additionally defines two nonstandard codes:
/// <c>498</c>, which is an authentication failure (an invalid token) rather
/// than a client error, and <c>499</c>, which means the client or a proxy
/// closed the request and is treated as a cancellation.
/// </remarks>
internal static class CohereErrorMapping
{
    /// <summary>Maps an HTTP status code onto a normalized failure kind.</summary>
    /// <param name="statusCode">The HTTP status code the provider returned.</param>
    /// <returns>The normalized failure kind.</returns>
    public static ProviderFailureKind MapStatusCode(HttpStatusCode statusCode) =>
        (int) statusCode switch
        {
            401 => ProviderFailureKind.Authentication,
            498 => ProviderFailureKind.Authentication,
            403 or 402 => ProviderFailureKind.Authorization,
            429 => ProviderFailureKind.Throttling,
            499 => ProviderFailureKind.Cancellation,
            400 or 404 or 422 or 501 => ProviderFailureKind.InvalidRequest,
            >= 500 => ProviderFailureKind.Unavailable,
            >= 400 and < 500 => ProviderFailureKind.InvalidRequest,
            _ => ProviderFailureKind.Unknown,
        };
}
