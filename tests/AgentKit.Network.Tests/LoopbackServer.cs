// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;

internal sealed class LoopbackServer: IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _serve;
    private int _acceptedConnections;

    private LoopbackServer(Func<NetworkStream, CancellationToken, Task> handle, int maxConnections = 1)
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint) _listener.LocalEndpoint).Port;
        _serve = ServeManyAsync(handle, maxConnections);
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

    /// <summary>
    /// Starts a server that accepts up to <paramref name="maxConnections"/> connections and answers
    /// every request it reads on each accepted connection with <paramref name="response"/> (which must
    /// not itself request that the connection close), so a client that keeps a connection alive keeps
    /// receiving responses on it. Used to observe whether a transport actually opens one connection per
    /// send or reuses a pooled one, independent of what the response headers ask for.
    /// </summary>
    internal static LoopbackServer StartKeepAlive(string response, int maxConnections) => new(
        async (stream, cancellationToken) =>
        {
            var buffer = new byte[4_096];
            while (true)
            {
                int read;
                try
                {
                    read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    return;
                }

                if (read == 0)
                {
                    return;
                }

                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken).ConfigureAwait(false);
            }
        },
        maxConnections);

    private async Task ServeManyAsync(Func<NetworkStream, CancellationToken, Task> handle, int maxConnections)
    {
        var connections = new List<Task>();
        for (var i = 0; i < maxConnections; i++)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_shutdown.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }

            _ = Interlocked.Increment(ref _acceptedConnections);
            connections.Add(HandleConnectionAsync(client, handle));
        }

        await Task.WhenAll(connections).ConfigureAwait(false);
    }

    private async Task HandleConnectionAsync(TcpClient client, Func<NetworkStream, CancellationToken, Task> handle)
    {
        using (client)
        await using (var stream = client.GetStream())
        {
            try
            {
                await handle(stream, _shutdown.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
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
