// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using System.IO.Pipelines;

using ModelContextProtocol.Protocol;

/// <summary>Opens in-memory pipe MCP transports for deterministic tests and loopback hosts.</summary>
public sealed class PipeMcpTransportFactory: IMcpTransportFactory
{
    private readonly Func<CancellationToken, ValueTask<PipeMcpTransportPair>> _pairFactory;

    /// <summary>Initializes a factory that creates client/server pipe pairs on demand.</summary>
    /// <param name="pairFactory">Creates one connected pipe pair per open request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pairFactory"/> is null.</exception>
    public PipeMcpTransportFactory(Func<CancellationToken, ValueTask<PipeMcpTransportPair>> pairFactory)
    {
        ArgumentNullException.ThrowIfNull(pairFactory);
        _pairFactory = pairFactory;
    }

    /// <inheritdoc/>
    public Type TransportProfileType => typeof(McpStdioTransportProfile);

    /// <inheritdoc/>
    public async ValueTask<McpTransportOpenResult> OpenAsync(
        McpTransportOpenRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pair = await _pairFactory(cancellationToken).ConfigureAwait(false);
        var transport = new StreamClientTransport(pair.ClientInput, pair.ClientOutput);
        return new McpTransportOpened(new SdkMcpClientTransport(transport));
    }
}

/// <summary>One connected MCP pipe transport pair.</summary>
/// <param name="ClientInput">The client-to-server stream.</param>
/// <param name="ClientOutput">The server-to-client stream.</param>
/// <param name="ServerInput">The server-side input stream.</param>
/// <param name="ServerOutput">The server-side output stream.</param>
public sealed record PipeMcpTransportPair(
    Stream ClientInput,
    Stream ClientOutput,
    Stream ServerInput,
    Stream ServerOutput)
{
    /// <summary>Creates one connected duplex pipe pair.</summary>
    /// <returns>A connected pipe pair for client and server transports.</returns>
    public static PipeMcpTransportPair CreateConnected()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        return new PipeMcpTransportPair(
            clientToServer.Writer.AsStream(),
            serverToClient.Reader.AsStream(),
            clientToServer.Reader.AsStream(),
            serverToClient.Writer.AsStream());
    }
}
