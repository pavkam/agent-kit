// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using ModelContextProtocol.Client;

/// <summary>Owns one official SDK client transport until asynchronous disposal.</summary>
internal sealed class SdkMcpClientTransport(IClientTransport transport): IMcpTransport
{
    private int _disposed;

    /// <summary>Gets the underlying SDK transport used to connect an <see cref="McpClient"/>.</summary>
    internal IClientTransport ClientTransport
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return transport;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        return Interlocked.CompareExchange(ref _disposed, 1, 0) != 0
            ? ValueTask.CompletedTask
            : transport is IAsyncDisposable asyncDisposable
            ? asyncDisposable.DisposeAsync()
            : ValueTask.CompletedTask;
    }
}
