// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>One immutable MCP endpoint registration captured before a session opens.</summary>
/// <remarks>
/// Callers keep this value for the life of the sessions they open from it.
/// A later catalog publication with a higher <see cref="Revision"/> does not
/// retarget an already captured endpoint. Authentication is optional because
/// stdio endpoints have no HTTP credential; when present it is a reference,
/// not a secret.
/// </remarks>
public sealed record McpEndpoint
{
    /// <summary>Initializes a captured endpoint.</summary>
    /// <param name="key">The non-default configuration key.</param>
    /// <param name="revision">The positive publication generation.</param>
    /// <param name="transport">The closed stdio or HTTP transport profile.</param>
    /// <param name="authentication">The credential reference when the transport requires one; otherwise null.</param>
    /// <param name="bounds">The positive captured bounds.</param>
    /// <exception cref="ArgumentNullException"><paramref name="transport"/> or <paramref name="bounds"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="revision"/> is not positive.</exception>
    public McpEndpoint(
        McpEndpointKey key,
        McpEndpointRevision revision,
        McpTransportProfile transport,
        McpAuthenticationReference? authentication,
        McpEndpointBounds bounds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentOutOfRangeException.ThrowIfLessThan(revision.Value, 1, nameof(revision));
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(bounds);

        Key = key;
        Revision = revision;
        Transport = transport;
        Authentication = authentication;
        Bounds = bounds;
    }

    /// <summary>Gets the configuration key.</summary>
    public McpEndpointKey Key { get; }

    /// <summary>Gets the publication generation captured with this endpoint.</summary>
    public McpEndpointRevision Revision { get; }

    /// <summary>Gets the closed transport profile.</summary>
    public McpTransportProfile Transport { get; }

    /// <summary>Gets the credential reference when one is required.</summary>
    public McpAuthenticationReference? Authentication { get; }

    /// <summary>Gets the bounds captured with this endpoint.</summary>
    public McpEndpointBounds Bounds { get; }
}
