// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

/// <summary>
/// Maps Google's canonical <c>google.rpc.Status</c> status vocabulary onto
/// the normalized <see cref="ProviderFailureKind"/> taxonomy.
/// </summary>
/// <remarks>
/// <para>
/// Every Google API surface that returns a <c>google.rpc.Status</c> error
/// envelope, including the Gemini Developer API and Google Cloud Vertex AI,
/// uses the same canonical status strings, so this table is shared by every
/// Google-hosted AgentKit provider rather than duplicated per package. It
/// maps only the status string; HTTP status fallback, message evidence, and
/// <c>Retry-After</c> handling live in <see cref="GoogleApiErrorFailureFactory"/>.
/// </para>
/// <para>
/// This type is stateless and safe to call concurrently.
/// </para>
/// </remarks>
public static class GoogleGeminiErrorMapping
{
    /// <summary>Maps a Google canonical status string onto a normalized failure kind.</summary>
    /// <param name="status">
    /// Google's <c>error.status</c> value, when available. Comparison is
    /// exact and case-sensitive, matching the canonical upper-case
    /// vocabulary Google emits.
    /// </param>
    /// <returns>
    /// The normalized failure kind, or <see cref="ProviderFailureKind.Unknown"/>
    /// when <paramref name="status"/> is <see langword="null"/>, empty, or
    /// not a recognized canonical status. Callers that hold an HTTP status
    /// should treat <see cref="ProviderFailureKind.Unknown"/> as "unmapped"
    /// and fall back to the HTTP status table rather than reporting it.
    /// </returns>
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
