// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory.Tests;

internal static class TestFactory
{
    public static NetworkDestination Destination(string host = "example.com", string scheme = "https", int port = 443, string route = "/") =>
        new(scheme, new NormalizedHost(host), port, new NetworkRoute(route));

    public static NetworkBounds Bounds() => new(
        connectTimeout: TimeSpan.FromSeconds(5),
        responseTimeout: TimeSpan.FromSeconds(30),
        maximumResponseBytes: 1_048_576,
        maximumRedirects: 5);

    public static NetworkResolutionRequest ResolutionRequest(NetworkDestination? destination = null) =>
        new(new NetworkOperationId(Guid.NewGuid()), destination ?? Destination(), Bounds());

    public static NetworkRequest Request(NetworkDestination? destination = null, NetworkMethod? method = null) =>
        new(
            new NetworkOperationId(Guid.NewGuid()),
            method ?? NetworkMethod.Get,
            destination ?? Destination(),
            NetworkHeaderSet.Empty,
            content: null,
            Bounds(),
            [Address()]);

    public static NetworkAddress Address(string ip = "93.184.216.34") =>
        new(IPAddress.Parse(ip), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5));

    public static NetworkResponseMetadata Metadata(int statusCode = 200) => new(statusCode, NetworkHeaderSet.Empty, contentLength: null);

    public static ScriptedNetworkNameResolver Resolver(TimeProvider? timeProvider = null) =>
        new(timeProvider ?? TimeProvider.System);

    public static ScriptedNetworkTransport Transport(TimeProvider? timeProvider = null, NetworkDestinationPolicy? policy = null) =>
        new(timeProvider ?? TimeProvider.System, policy);
}
