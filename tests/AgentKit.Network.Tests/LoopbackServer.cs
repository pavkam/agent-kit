// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;

internal sealed class LoopbackServer: IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly Task _serve;

    private LoopbackServer(string response)
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint) _listener.LocalEndpoint).Port;
        _serve = ServeAsync(response);
    }

    internal int Port { get; }

    internal static LoopbackServer Start(string response) => new(response);

    private async Task ServeAsync(string response)
    {
        using var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
        await using var stream = client.GetStream();
        var buffer = new byte[4_096];
        _ = await stream.ReadAsync(buffer).ConfigureAwait(false);
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response)).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        try
        {
            await _serve.ConfigureAwait(false);
        }
        catch (SocketException)
        {
        }
    }
}
