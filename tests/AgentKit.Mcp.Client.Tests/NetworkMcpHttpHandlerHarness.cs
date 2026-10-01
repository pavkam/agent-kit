// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

using System.Net.Http;

/// <summary>Composes <see cref="NetworkMcpHttpHandler"/> over deterministic security and network fakes.</summary>
internal sealed class NetworkMcpHttpHandlerHarness: IDisposable
{
    internal static readonly Uri Endpoint = new("https://mcp.example/rpc");

    private NetworkMcpHttpHandlerHarness(
        NetworkMcpHttpHandler handler,
        ConsumingGrantStore grants,
        GrantingSecurityAuthority authority,
        FixedAddressNameResolver resolver,
        INetworkTransport transport,
        RecordingLogger<NetworkMcpHttpHandler> logger,
        McpClientOptionsSnapshot options,
        McpEndpoint endpoint)
    {
        Handler = handler;
        Grants = grants;
        Authority = authority;
        Resolver = resolver;
        Transport = transport;
        Logger = logger;
        Options = options;
        McpEndpoint = endpoint;
        Client = new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
    }

    internal NetworkMcpHttpHandler Handler { get; }

    internal HttpClient Client { get; }

    internal ConsumingGrantStore Grants { get; }

    internal GrantingSecurityAuthority Authority { get; }

    internal FixedAddressNameResolver Resolver { get; }

    internal INetworkTransport Transport { get; }

    internal RecordingLogger<NetworkMcpHttpHandler> Logger { get; }

    internal McpClientOptionsSnapshot Options { get; }

    internal McpEndpoint McpEndpoint { get; }

    internal IReadOnlyList<NetworkRequest> Requests => Transport switch
    {
        HandlerNetworkTransport handler => handler.Requests,
        CallbackNetworkTransport callback => callback.Requests,
        _ => [],
    };

    internal static McpClientOptionsSnapshot CreateOptions(Action<McpClientOptions>? configure = null)
    {
        var state = new McpClientOptionsState();
        state.Configure(options =>
        {
            options.HandshakeTimeout = TimeSpan.FromSeconds(5);
            options.RequestTimeout = TimeSpan.FromSeconds(30);
            options.ShutdownTimeout = TimeSpan.FromSeconds(3);
            options.MaximumFrameBytes = 1024;
            options.MaximumMessageBytes = 4096;
            options.MaximumInFlightRequests = 4;
            options.HttpStreamTimeout = TimeSpan.FromMinutes(7);
            options.MaximumHttpResponseBytes = 8192;
            configure?.Invoke(options);
        });
        return state.Snapshot;
    }

    internal static McpEndpoint CreateEndpoint(McpClientOptionsSnapshot options, Uri? endpoint = null) =>
        new(
            new McpEndpointKey("remote"),
            new McpEndpointRevision(1),
            new McpHttpTransportProfile(endpoint ?? Endpoint),
            authentication: null,
            options.CreateEndpointBounds());

    internal static NetworkMcpHttpHandlerHarness Create(
        HttpMessageHandler responder,
        Action<McpClientOptions>? configure = null,
        ISecurityAuthoritySelector? selector = null,
        TimeProvider? timeProvider = null) =>
        Create(grants => new HandlerNetworkTransport(responder, grants), configure, selector, timeProvider);

    internal static NetworkMcpHttpHandlerHarness Create(
        Func<ISecurityGrantStore, INetworkTransport> transportFactory,
        Action<McpClientOptions>? configure = null,
        ISecurityAuthoritySelector? selector = null,
        TimeProvider? timeProvider = null)
    {
        var clock = timeProvider ?? TimeProvider.System;
        var grants = new ConsumingGrantStore();
        var authority = new GrantingSecurityAuthority(grants);
        var resolver = new FixedAddressNameResolver(grants, clock);
        var transport = transportFactory(grants);
        var logger = new RecordingLogger<NetworkMcpHttpHandler>();
        var options = CreateOptions(configure);
        var endpoint = CreateEndpoint(options);
        var handler = new NetworkMcpHttpHandler(
            resolver,
            transport,
            selector ?? new FixedSecurityAuthoritySelector(authority),
            new SequentialGenerator<SecurityRequestId>(static value => new SecurityRequestId(value), "22000000"),
            new SequentialGenerator<NetworkOperationId>(static value => new NetworkOperationId(value), "32000000"),
            ProviderEgressHarness.Operation,
            endpoint,
            options,
            clock,
            logger);
        return new NetworkMcpHttpHandlerHarness(handler, grants, authority, resolver, transport, logger, options, endpoint);
    }

    public void Dispose()
    {
        Client.Dispose();
        Handler.Dispose();
    }

    private sealed class SequentialGenerator<TId>(Func<Guid, TId> factory, string prefix): IIdentifierGenerator<TId>
        where TId : struct
    {
        private int _value;

        public TId Create() => factory(Guid.Parse($"{prefix}-0000-0000-0000-{Interlocked.Increment(ref _value):D12}"));
    }
}
