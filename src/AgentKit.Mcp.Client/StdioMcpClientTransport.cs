// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using ModelContextProtocol.Client;

/// <summary>Owns stdio MCP streams and the underlying process handle until disposal.</summary>
internal sealed class StdioMcpClientTransport(McpProcessHandleStreams streams, IClientTransport clientTransport): IMcpTransport
{
    private int _disposed;

    internal IClientTransport ClientTransport
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return clientTransport;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        if (clientTransport is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }

        await streams.DisposeAsync().ConfigureAwait(false);
    }
}
