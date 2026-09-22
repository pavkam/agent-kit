// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

using System.Collections.Concurrent;
using System.Net;

/// <summary>Owns route-partitioned <see cref="SocketsHttpHandler"/> instances.</summary>
internal sealed class NetworkTransportHandlerPool: IDisposable
{
    internal static HttpRequestOptionsKey<IPAddress> ResolvedAddressKey { get; } = new("AgentKit.Network.ResolvedAddress");

    internal static HttpRequestOptionsKey<TimeSpan> ConnectTimeoutKey { get; } = new("AgentKit.Network.ConnectTimeout");
    private readonly ConcurrentDictionary<NetworkConnectionPartitionKey, HttpMessageInvoker> _invokers = new();
    private readonly AgentNetworkOptionsSnapshot _options;
    private readonly TimeProvider _timeProvider;
    private bool _disposed;

    internal NetworkTransportHandlerPool(AgentNetworkOptionsSnapshot options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _options = options;
        _timeProvider = timeProvider;
    }

    internal HttpMessageInvoker GetInvoker(NetworkConnectionPartitionKey partitionKey) =>
        _invokers.GetOrAdd(partitionKey, static (_, state) => CreateInvoker(state), new PoolState(_options, _timeProvider));

    private static HttpMessageInvoker CreateInvoker(PoolState state)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            MaxResponseHeadersLength = state.Options.MaximumResponseHeaderKilobytes,
            AutomaticDecompression = state.Options.DecompressionPolicy == NetworkDecompressionPolicy.AllowAutomatic
                ? DecompressionMethods.All
                : DecompressionMethods.None,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = (context, cancellationToken) =>
                ConnectAsync(context, state.TimeProvider, cancellationToken),
        };
        if (state.Options.Proxy.UseProxy && state.Options.Proxy.ProxyUri is { } proxyUri)
        {
            handler.Proxy = new WebProxy(proxyUri);
            handler.UseProxy = true;
        }

        return new HttpMessageInvoker(handler, disposeHandler: true);
    }

    private readonly record struct PoolState(AgentNetworkOptionsSnapshot Options, TimeProvider TimeProvider);

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!context.InitialRequestMessage.Options.TryGetValue(ResolvedAddressKey, out var address))
        {
            throw new NetworkConnectFailedException("No authorized resolved address was attached.");
        }

        if (!context.InitialRequestMessage.Options.TryGetValue(ConnectTimeoutKey, out var connectTimeout))
        {
            throw new NetworkConnectFailedException("No authorized connection deadline was attached.");
        }

        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        using var timeout = new CancellationTokenSource(connectTimeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await socket.ConnectAsync(address, context.DnsEndPoint.Port, linked.Token).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch (OperationCanceledException exception)
            when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            socket.Dispose();
            throw new NetworkConnectFailedException(
                "The pinned-address connection exceeded its deadline.",
                NetworkFailureKind.Timeout,
                exception);
        }
        catch (OperationCanceledException)
        {
            socket.Dispose();
            throw;
        }
        catch (SocketException exception)
        {
            socket.Dispose();
            throw new NetworkConnectFailedException(
                "The pinned-address connection failed.",
                NetworkFailureKind.ConnectionFailed,
                exception);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var invoker in _invokers.Values)
        {
            invoker.Dispose();
        }

        _invokers.Clear();
    }
}
