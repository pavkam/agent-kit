// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using System.Net;

using AgentKit.Providers.Http;

/// <summary>
/// Maps OpenAI-compatible error metadata onto the portable
/// <see cref="ProviderFailureKind"/> taxonomy before falling back to HTTP status.
/// </summary>
internal static class OpenAIProviderFailureKindMapper
{
    /// <summary>Maps one streaming or body-only error onto a normalized failure kind.</summary>
    /// <param name="errorType">The provider's <c>error.type</c> or metadata fallback.</param>
    /// <param name="errorCode">The provider's <c>error.code</c>, when present.</param>
    /// <returns>The normalized failure kind, or <see cref="ProviderFailureKind.Unknown"/> when unmapped.</returns>
    public static ProviderFailureKind MapFromErrorBody(string? errorType, string? errorCode)
    {
        return IsContextLengthExceeded(errorType, errorCode)
            ? ProviderFailureKind.ContextLengthExceeded
            : (errorType ?? errorCode) switch
            {
                "rate_limit_error" or "rate_limit_exceeded" or "insufficient_quota" => ProviderFailureKind.Throttling,
                "authentication_error" => ProviderFailureKind.Authentication,
                "permission_error" => ProviderFailureKind.Authorization,
                "invalid_request_error" => ProviderFailureKind.InvalidRequest,
                "server_error" or "overloaded_error" => ProviderFailureKind.Unavailable,
                _ => ProviderFailureKind.Unknown,
            };
    }

    /// <summary>Maps an HTTP response using parsed provider codes when available.</summary>
    /// <param name="statusCode">The HTTP status returned by the provider.</param>
    /// <param name="providerCode">The provider's machine-readable error code, if any.</param>
    /// <returns>The normalized failure kind.</returns>
    public static ProviderFailureKind Map(HttpStatusCode statusCode, string? providerCode) =>
        IsContextLengthExceeded(errorType: null, providerCode)
            ? ProviderFailureKind.ContextLengthExceeded
            : HttpStatusFailureKindMapper.Map(statusCode);

    private static bool IsContextLengthExceeded(string? errorType, string? errorCode) =>
        string.Equals(errorCode, "context_length_exceeded", StringComparison.OrdinalIgnoreCase)
        || string.Equals(errorType, "context_length_exceeded", StringComparison.OrdinalIgnoreCase)
        || string.Equals(errorCode, "string_above_max_length", StringComparison.OrdinalIgnoreCase);
}
