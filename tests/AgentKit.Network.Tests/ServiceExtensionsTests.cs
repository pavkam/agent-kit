// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public async Task AddAgentNetwork_WhenIntentGeneratorIsHostSupplied_UsesTheReplacement()
    {
        var store = new TestGrantStore();
        var expectedId = new SecurityEnforcementIntentId(Guid.Parse("81000000-0000-0000-0000-000000000008"));
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(new SequenceSecurityEnforcementIntentIdGenerator(expectedId.Value));
        _ = services.AddAgentNetwork(value => value.DestinationPolicy = new NetworkDestinationPolicy(["http"], null, allowPrivateAddresses: true));
        using var provider = services.BuildServiceProvider();
        _ = await provider.GetRequiredService<INetworkNameResolver>().ResolveAsync(ResolutionRequest(Destination(443)), TestContext.Current.CancellationToken);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(expectedId);
    }

    [Fact]
    public async Task AddAgentNetwork_WhenTransportResolved_ConsumesGrantThroughTheRegisteredTransport()
    {
        var store = new TestGrantStore();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddAgentNetwork(value => value.DestinationPolicy = new NetworkDestinationPolicy(["http"], null, allowPrivateAddresses: true));
        using var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredService<INetworkTransport>();
        _ = await transport.SendAsync(Request(Destination(1)), TestContext.Current.CancellationToken);
        _ = store.Enforcements.ShouldHaveSingleItem();
    }

    private static NetworkRequest Request(NetworkDestination destination) => new(
        new NetworkOperationId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
        NetworkMethod.Get,
        destination,
        NetworkHeaderSet.Empty,
        null,
        Bounds(),
        [new NetworkAddress(IPAddress.Loopback, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1))],
        NetworkDataClassification.Public,
        TestSecurity.Grant());

    private static NetworkDestination Destination(int port) => new("http", new NormalizedHost("127.0.0.1"), port, NetworkRoute.Root);
    private static NetworkBounds Bounds(TimeSpan? responseTimeout = null, long maximumResponseBytes = 1_024) => new(TimeSpan.FromSeconds(2), responseTimeout ?? TimeSpan.FromSeconds(2), maximumResponseBytes, 3);
    private static NetworkResolutionRequest ResolutionRequest(NetworkDestination destination) => new(new NetworkOperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")), destination, Bounds(), TestSecurity.Grant());
}
