// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>A network response over a caller-supplied body stream, for fragmentation, fault, and gating scenarios.</summary>
public sealed class StreamNetworkResponse: INetworkResponse
{
    /// <summary>Initializes a response with a status, ordered headers, and an owned body stream.</summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="content">The body stream; the response owns and disposes it.</param>
    /// <param name="headers">The ordered response headers.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    public StreamNetworkResponse(int statusCode, Stream content, params IEnumerable<NetworkHeader> headers)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
        Metadata = new NetworkResponseMetadata(statusCode, new NetworkHeaderSet([.. headers]), contentLength: null);
    }

    /// <inheritdoc/>
    public NetworkResponseMetadata Metadata { get; }

    /// <inheritdoc/>
    public NetworkEgressEvidence? EgressEvidence => null;

    /// <inheritdoc/>
    public Stream Content { get; }

    /// <summary>Gets a value indicating whether <see cref="DisposeAsync"/> ran.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        IsDisposed = true;
        await Content.DisposeAsync().ConfigureAwait(false);
    }
}
