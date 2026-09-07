// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

using System.Net;

/// <summary>
/// Maps a Mistral AI HTTP error status code onto the normalized
/// <see cref="ProviderFailureKind"/> taxonomy.
/// </summary>
/// <remarks>
/// Mistral AI's documented error contract does not expose a canonical
/// machine-readable error-type vocabulary comparable to Anthropic's
/// <c>error.type</c> or Google's <c>error.status</c>; only the HTTP status
/// code and a human-readable <c>detail</c> are guaranteed, so this mapping
/// is HTTP-status-driven rather than error-code-driven.
/// </remarks>
internal static class MistralAIErrorMapping
{
    /// <summary>Maps an HTTP status code onto a normalized failure kind.</summary>
    /// <param name="statusCode">The HTTP status code the provider returned.</param>
    /// <returns>The normalized failure kind.</returns>
    public static ProviderFailureKind MapStatusCode(HttpStatusCode statusCode) =>
        (int) statusCode switch
        {
            401 => ProviderFailureKind.Authentication,
            403 => ProviderFailureKind.Authorization,
            429 => ProviderFailureKind.Throttling,
            408 => ProviderFailureKind.Timeout,
            400 or 404 or 422 => ProviderFailureKind.InvalidRequest,
            >= 500 => ProviderFailureKind.Unavailable,
            >= 400 and < 500 => ProviderFailureKind.InvalidRequest,
            _ => ProviderFailureKind.Unknown,
        };
}
