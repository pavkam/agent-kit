// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using System.Net.Http.Headers;

/// <summary>
/// The credential resolved successfully into a usable HTTP
/// <c>Authorization</c> header value.
/// </summary>
public sealed record OpenAIAuthorizationGranted: OpenAIAuthorizationResult
{
    /// <summary>Initializes a new instance of the <see cref="OpenAIAuthorizationGranted"/> record.</summary>
    /// <param name="authorization">The <c>Authorization</c> header value to attach to the request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="authorization"/> is null.</exception>
    public OpenAIAuthorizationGranted(AuthenticationHeaderValue authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        Authorization = authorization;
    }

    /// <summary>Gets the <c>Authorization</c> header value to attach to the request.</summary>
    public AuthenticationHeaderValue Authorization { get; init; }
}
