// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

/// <summary>
/// Maps Google's canonical <c>google.rpc.Status</c> status vocabulary onto
/// the normalized <see cref="ProviderFailureKind"/> taxonomy.
/// </summary>
/// <remarks>
/// When no canonical status string is available the adapters fall back to
/// the shared HTTP status table in
/// <see cref="Http.HttpStatusFailureKindMapper"/>; Vertex AI has
/// no provider-specific HTTP status semantics beyond that table.
/// </remarks>
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
}
