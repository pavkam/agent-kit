// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI;

using System.Net;

/// <summary>
/// Maps an Azure OpenAI HTTP error status code onto the normalized
/// <see cref="ProviderFailureKind"/> taxonomy.
/// </summary>
/// <remarks>
/// Azure documents 400, 401, 403, 404, 408, 429, and transient 5xx
/// responses for the GA v1 Chat Completions dialect. A 401/403 can
/// indicate an invalid resource API key or a Microsoft Entra token with
/// the wrong audience or role; this package classifies both as
/// authentication/authorization failures and leaves audience/role
/// diagnosis to the caller, since that requires token introspection this
/// package does not perform.
/// </remarks>
internal static class AzureOpenAIErrorMapping
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
            408 or 504 => ProviderFailureKind.Timeout,
            >= 400 and < 500 => ProviderFailureKind.InvalidRequest,
            >= 500 => ProviderFailureKind.Unavailable,
            _ => ProviderFailureKind.Unknown,
        };
}
