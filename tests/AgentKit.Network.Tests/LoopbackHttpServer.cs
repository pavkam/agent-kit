// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;

/// <summary>A minimal loopback-only HTTP server for exercising <see cref="DefaultNetworkTransport"/> without public network access.</summary>
internal sealed class LoopbackHttpServer: IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly Task _acceptLoop;
    private readonly CancellationTokenSource _stopSource = new();
    private readonly Func<HttpListenerRequest, HttpListenerResponseSpec> _handler;

    private LoopbackHttpServer(int port, Func<HttpListenerRequest, HttpListenerResponseSpec> handler)
    {
        Port = port;
        _handler = handler;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    public int Port { get; }

    public static LoopbackHttpServer Start(Func<HttpListenerRequest, HttpListenerResponseSpec> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var port = ReserveFreePort();
        return new LoopbackHttpServer(port, handler);
    }

    public async ValueTask DisposeAsync()
    {
        await _stopSource.CancelAsync();
        _listener.Stop();
        try
        {
            await _acceptLoop.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Swallow: the accept loop's own listener-closed exception is expected on shutdown.
        }

        _listener.Close();
        _stopSource.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stopSource.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(_stopSource.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return;
            }

            var spec = _handler(context.Request);
            context.Response.StatusCode = spec.StatusCode;
            foreach (var header in spec.Headers)
            {
                context.Response.Headers.Add(header.Name, header.Value);
            }

            if (spec.ContentLength is { } length)
            {
                context.Response.ContentLength64 = length;
            }

            await context.Response.OutputStream.WriteAsync(spec.Body).ConfigureAwait(false);
            context.Response.OutputStream.Close();
        }
    }

    private static int ReserveFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint) probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}

/// <summary>Describes one scripted loopback server response.</summary>
internal sealed class HttpListenerResponseSpec
{
    public HttpListenerResponseSpec(int statusCode, ImmutableArray<NetworkHeader> headers, ReadOnlyMemory<byte> body, long? contentLength)
    {
        StatusCode = statusCode;
        Headers = headers;
        Body = body;
        ContentLength = contentLength;
    }

    public int StatusCode { get; }

    public ImmutableArray<NetworkHeader> Headers { get; }

    public ReadOnlyMemory<byte> Body { get; }

    public long? ContentLength { get; }
}
