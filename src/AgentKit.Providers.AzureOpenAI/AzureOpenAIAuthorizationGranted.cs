// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI;

/// <summary>
/// The credential resolved successfully into one usable HTTP authentication
/// header.
/// </summary>
/// <remarks>
/// Unlike the public OpenAI API (and the shared
/// <c>AgentKit.Providers.OpenAICompatible</c> base, which always sends
/// <c>Authorization: Bearer</c>), Azure OpenAI distinguishes its two
/// credential kinds by header shape: a resource API key is sent as
/// <c>api-key: &lt;key&gt;</c> with no scheme prefix, while a Microsoft
/// Entra access token is sent as a standard
/// <c>Authorization: Bearer &lt;token&gt;</c> header. <see cref="HeaderName"/>
/// and <see cref="HeaderValue"/> together express whichever shape the
/// resolved credential actually requires.
/// </remarks>
public sealed record AzureOpenAIAuthorizationGranted: AzureOpenAIAuthorizationResult
{
    /// <summary>Initializes a new instance of the <see cref="AzureOpenAIAuthorizationGranted"/> record.</summary>
    /// <param name="headerName">The name of the authentication header to attach to the request.</param>
    /// <param name="headerValue">The value of the authentication header to attach to the request.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="headerName"/> or <paramref name="headerValue"/> is
    /// null, empty, or consists only of whitespace.
    /// </exception>
    public AzureOpenAIAuthorizationGranted(string headerName, string headerValue)
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
