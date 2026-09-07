// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

using System.Net;

/// <summary>
/// Maps Google's canonical <c>google.rpc.Status</c> status vocabulary, or
/// failing that an HTTP status code, onto the normalized
/// <see cref="ProviderFailureKind"/> taxonomy.
/// </summary>
internal static class GoogleVertexAIErrorMapping
{
    /// <summary>Maps a Google canonical status string onto a normalized failure kind.</summary>
    /// <param name="status">Google's <c>error.status</c> value, when available.</param>
    /// <returns>The normalized failure kind.</returns>
    public static ProviderFailureKind MapStatus(string? status) =>
        status switch
        {
            "INVALID_ARGUMENT" or "FAILED_PRECONDITION" or "OUT_OF_RANGE" => ProviderFailureKind.InvalidRequest,
            "UNAUTHENTICATED" => ProviderFailureKind.Authentication,
            "PERMISSION_DENIED" => ProviderFailureKind.Authorization,
            "NOT_FOUND" => ProviderFailureKind.InvalidRequest,
            "RESOURCE_EXHAUSTED" => ProviderFailureKind.Throttling,
            "DEADLINE_EXCEEDED" => ProviderFailureKind.Timeout,
            "UNAVAILABLE" or "INTERNAL" or "ABORTED" => ProviderFailureKind.Unavailable,
            "CANCELLED" => ProviderFailureKind.Cancellation,
            _ => ProviderFailureKind.Unknown,
        };

    /// <summary>Maps an HTTP status code onto a normalized failure kind, used when no canonical status string is available.</summary>
    /// <param name="statusCode">The HTTP status code the provider returned.</param>
    /// <returns>The normalized failure kind.</returns>
    public static ProviderFailureKind MapStatusCode(HttpStatusCode statusCode) =>
        (int) statusCode switch
        {
            401 => ProviderFailureKind.Authentication,
            403 => ProviderFailureKind.Authorization,
            429 => ProviderFailureKind.Throttling,
            400 or 404 => ProviderFailureKind.InvalidRequest,
            >= 500 => ProviderFailureKind.Unavailable,
            >= 400 and < 500 => ProviderFailureKind.InvalidRequest,
            _ => ProviderFailureKind.Unknown,
        };
}
