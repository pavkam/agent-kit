// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;

internal static class TestFactory
{
    public static NetworkBounds Bounds(
        TimeSpan? connectTimeout = null, TimeSpan? responseTimeout = null, long maximumResponseBytes = 1_048_576, int maximumRedirects = 5) =>
        new(
            connectTimeout ?? TimeSpan.FromSeconds(5),
            responseTimeout ?? TimeSpan.FromSeconds(5),
            maximumResponseBytes,
            maximumRedirects);

    public static NetworkDestination LoopbackDestination(int port, string route = "/", string scheme = "http") =>
        new(scheme, new NormalizedHost("127.0.0.1"), port, new NetworkRoute(route));

    public static NetworkAddress Address(string ip = "127.0.0.1", TimeProvider? timeProvider = null)
    {
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        return new NetworkAddress(IPAddress.Parse(ip), now, now.AddMinutes(5));
    }

    public static NetworkResolutionRequest ResolutionRequest(
        NetworkDestination destination,
        NetworkBounds? bounds = null,
        NetworkOperationId? id = null) => new(
            id ?? new NetworkOperationId(Guid.NewGuid()),
            destination,
            bounds ?? Bounds());

    public static NetworkRequest Request(
        NetworkDestination destination,
        NetworkMethod? method = null,
        NetworkBounds? bounds = null,
        TimeProvider? timeProvider = null) =>
        new(
            new NetworkOperationId(Guid.NewGuid()),
            method ?? NetworkMethod.Get,
            destination,
            NetworkHeaderSet.Empty,
            content: null,
            bounds ?? Bounds(),
            [Address(timeProvider: timeProvider)]);

    public static DefaultNetworkNameResolver Resolver(NetworkDestinationPolicy? policy = null, TimeProvider? timeProvider = null) =>
        new(
            timeProvider ?? TimeProvider.System,
            Options.Create(new AgentNetworkOptions
            {
                DestinationPolicy = policy ?? AllowLoopbackHttpPolicy(),
            }));

    public static DefaultNetworkTransport Transport(NetworkDestinationPolicy? policy = null, TimeProvider? timeProvider = null) =>
        new(
            timeProvider ?? TimeProvider.System,
            Options.Create(new AgentNetworkOptions
            {
                DestinationPolicy = policy ?? AllowLoopbackHttpPolicy(),
            }));

    public static NetworkDestinationPolicy AllowLoopbackHttpPolicy() => new(["http"], null, allowPrivateAddresses: true);
}
