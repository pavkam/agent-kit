// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic;

/// <summary>
/// The credential resolved successfully into one usable HTTP authentication
/// header.
/// </summary>
/// <remarks>
/// Unlike OpenAI-compatible endpoints, Anthropic does not authenticate
/// every credential kind through the same header: an API key is sent as
/// <c>x-api-key</c>, while an OAuth access token (for example, from
/// Workload Identity Federation) is sent as a standard
/// <c>Authorization: Bearer</c> header. <see cref="HeaderName"/> and
/// <see cref="HeaderValue"/> together express whichever header the
/// resolved credential actually requires.
/// </remarks>
public sealed record AnthropicAuthorizationGranted: AnthropicAuthorizationResult
{
    /// <summary>Initializes a new instance of the <see cref="AnthropicAuthorizationGranted"/> record.</summary>
    /// <param name="headerName">The name of the authentication header to attach to the request.</param>
    /// <param name="headerValue">The value of the authentication header to attach to the request.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="headerName"/> or <paramref name="headerValue"/> is
    /// null, empty, or consists only of whitespace.
    /// </exception>
    public AnthropicAuthorizationGranted(string headerName, string headerValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(headerValue);

        HeaderName = headerName;
        HeaderValue = headerValue;
    }

    /// <summary>Gets the name of the authentication header to attach to the request.</summary>
    public string HeaderName { get; init; }

    /// <summary>Gets the value of the authentication header to attach to the request.</summary>
    public string HeaderValue { get; init; }
}
