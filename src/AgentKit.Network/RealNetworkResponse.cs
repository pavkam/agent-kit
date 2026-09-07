// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>An <see cref="INetworkResponse"/> backed by a real <see cref="HttpResponseMessage"/> and its bounded content stream.</summary>
internal sealed class RealNetworkResponse: INetworkResponse
{
    private readonly HttpResponseMessage _response;
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="RealNetworkResponse"/> class.</summary>
    /// <param name="response">The owned underlying HTTP response.</param>
    /// <param name="content">The bounded response body stream.</param>
    /// <param name="metadata">The response metadata.</param>
    public RealNetworkResponse(HttpResponseMessage response, Stream content, NetworkResponseMetadata metadata)
    {
        _response = response;
        Content = content;
        Metadata = metadata;
    }

    /// <inheritdoc/>
    public NetworkResponseMetadata Metadata { get; }

    /// <inheritdoc/>
    public Stream Content { get; }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await Content.DisposeAsync().ConfigureAwait(false);
            _response.Dispose();
        }
    }
}
