// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;

internal sealed class LoopbackServer: IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _serve;
    private int _acceptedConnections;

    private LoopbackServer(Func<NetworkStream, CancellationToken, Task> handle)
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint) _listener.LocalEndpoint).Port;
        _serve = ServeAsync(handle);
    }

    internal int Port { get; }

    internal int AcceptedConnections => Volatile.Read(ref _acceptedConnections);

    internal static LoopbackServer Start(string response) => new(async (stream, cancellationToken) =>
    {
        var buffer = new byte[4_096];
        _ = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken).ConfigureAwait(false);
    });

    /// <summary>Starts a server that accepts one connection, reads the request, and then never responds.</summary>
    internal static LoopbackServer StartHanging() => new(async (stream, cancellationToken) =>
    {
        var buffer = new byte[4_096];
        _ = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
    });

    private async Task ServeAsync(Func<NetworkStream, CancellationToken, Task> handle)
    {
        using var client = await _listener.AcceptTcpClientAsync(_shutdown.Token).ConfigureAwait(false);
        _ = Interlocked.Increment(ref _acceptedConnections);
        await using var stream = client.GetStream();
        try
        {
            await handle(stream, _shutdown.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        try
        {
            await _serve.ConfigureAwait(false);
        }
        catch (SocketException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _shutdown.Dispose();
        }
    }
}
