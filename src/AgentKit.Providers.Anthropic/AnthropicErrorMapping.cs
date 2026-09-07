// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic;

/// <summary>
/// Maps Anthropic's stable error <c>type</c> vocabulary, shared by HTTP
/// error bodies and in-stream <c>error</c> events, onto the normalized
/// <see cref="ProviderFailureKind"/> taxonomy.
/// </summary>
internal static class AnthropicErrorMapping
{
    /// <summary>Maps an Anthropic error type string onto a normalized failure kind.</summary>
    /// <param name="anthropicErrorType">Anthropic's <c>error.type</c> value, when available.</param>
    /// <returns>The normalized failure kind.</returns>
    public static ProviderFailureKind MapErrorType(string? anthropicErrorType) =>
        anthropicErrorType switch
        {
            "invalid_request_error" => ProviderFailureKind.InvalidRequest,
            "authentication_error" => ProviderFailureKind.Authentication,
            "permission_error" => ProviderFailureKind.Authorization,
            "not_found_error" => ProviderFailureKind.InvalidRequest,
            "request_too_large" => ProviderFailureKind.InvalidRequest,
            "rate_limit_error" => ProviderFailureKind.Throttling,
            "overloaded_error" => ProviderFailureKind.Unavailable,
            "api_error" => ProviderFailureKind.Unavailable,
            _ => ProviderFailureKind.Unknown,
        };
}
