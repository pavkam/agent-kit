// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Describes an HTTP MCP endpoint without opening a connection.</summary>
/// <remarks>
/// The URI must be absolute HTTP or HTTPS and must not carry user info.
/// Credentials are a separate <see cref="McpAuthenticationReference"/>. The
/// HTTP transport later sends through the network boundary under its own grant;
/// a redirect to a different audience requires reevaluation.
/// </remarks>
public sealed record McpHttpTransportProfile: McpTransportProfile
{
    /// <summary>Initializes an HTTP transport profile.</summary>
    /// <param name="endpoint">The absolute credential-free HTTP(S) endpoint.</param>
    /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="endpoint"/> is not an absolute credential-free HTTP(S) URI with a host.
    /// </exception>
    public McpHttpTransportProfile(Uri endpoint)
    {
        ArgumentException.ThrowIfInvalidWebResultUri(endpoint);
        Endpoint = endpoint;
    }

    /// <summary>Gets the absolute credential-free HTTP(S) endpoint.</summary>
    public Uri Endpoint { get; }
}
