// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory;

/// <summary>An <see cref="INetworkResponse"/> backed by an in-memory scripted body.</summary>
public sealed class ScriptedNetworkResponse: INetworkResponse
{
    private readonly MemoryStream _content;
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="ScriptedNetworkResponse"/> class.</summary>
    /// <param name="metadata">The scripted response metadata.</param>
    /// <param name="body">The scripted response body bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="metadata"/> is null.</exception>
    public ScriptedNetworkResponse(NetworkResponseMetadata metadata, ReadOnlyMemory<byte> body)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Metadata = metadata;
        _content = new MemoryStream(body.ToArray(), writable: false);
    }

    /// <inheritdoc/>
    public NetworkResponseMetadata Metadata { get; }

    /// <inheritdoc/>
    public Stream Content => _content;

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _content.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
