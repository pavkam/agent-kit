// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

/// <summary>
/// The credential resolved successfully into one usable HTTP authentication
/// header.
/// </summary>
/// <remarks>
/// Cohere has exactly one authentication mechanism: a bearer token sent as
/// <c>Authorization: Bearer &lt;value&gt;</c>. Both a static API key and an
/// OAuth access token are sent through the identical header shape, so
/// <see cref="HeaderName"/> is always <c>"Authorization"</c> here; the
/// distinction between the two credential kinds is only which token value
/// <see cref="CohereAuthorizationHeaderFactory"/> selected and whether it
/// checked an expiry.
/// </remarks>
public sealed record CohereAuthorizationGranted: CohereAuthorizationResult
{
    /// <summary>Initializes a new instance of the <see cref="CohereAuthorizationGranted"/> record.</summary>
    /// <param name="headerName">The name of the authentication header to attach to the request.</param>
    /// <param name="headerValue">The value of the authentication header to attach to the request.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="headerName"/> or <paramref name="headerValue"/> is
    /// null, empty, or consists only of whitespace.
    /// </exception>
    public CohereAuthorizationGranted(string headerName, string headerValue)
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
