// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

/// <summary>
/// The credential resolved successfully into one usable HTTP authentication
/// header.
/// </summary>
/// <remarks>
/// Vertex AI has exactly one authentication mechanism: a Google OAuth 2.0
/// access token (typically from Application Default Credentials) sent as
/// <c>Authorization: Bearer &lt;token&gt;</c>. <see cref="HeaderName"/> is
/// therefore always <c>"Authorization"</c> here.
/// </remarks>
public sealed record GoogleVertexAIAuthorizationGranted: GoogleVertexAIAuthorizationResult
{
    /// <summary>Initializes a new instance of the <see cref="GoogleVertexAIAuthorizationGranted"/> record.</summary>
    /// <param name="headerName">The name of the authentication header to attach to the request.</param>
    /// <param name="headerValue">The value of the authentication header to attach to the request.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="headerName"/> or <paramref name="headerValue"/> is
    /// null, empty, or consists only of whitespace.
    /// </exception>
    public GoogleVertexAIAuthorizationGranted(string headerName, string headerValue)
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
