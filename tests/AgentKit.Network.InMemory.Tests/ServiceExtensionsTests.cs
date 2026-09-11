// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public async Task AddAgentNetworkInMemory_WhenIntentGeneratorIsHostSupplied_UsesTheReplacement()
    {
        var store = new TestGrantStore();
        var expectedId = new SecurityEnforcementIntentId(Guid.Parse("81000000-0000-0000-0000-000000000008"));
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(new SequenceSecurityEnforcementIntentIdGenerator(expectedId.Value));
        _ = services.AddAgentNetworkInMemory();
        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<INetworkNameResolver>().ShouldBeOfType<ScriptedNetworkNameResolver>();
        resolver.Script(Destination(), new NetworkResolved([Address()]));
        _ = await resolver.ResolveAsync(ResolutionRequest(), TestContext.Current.CancellationToken);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(expectedId);
    }

    [Fact]
    public void AddAgentNetworkInMemory_WhenCalledTwice_UsesOneSharedScriptedBoundary()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore, TestGrantStore>();
        _ = services.AddAgentNetworkInMemory().AddAgentNetworkInMemory();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<INetworkNameResolver>().ShouldBeSameAs(provider.GetRequiredService<ScriptedNetworkNameResolver>());
        provider.GetRequiredService<INetworkTransport>().ShouldBeSameAs(provider.GetRequiredService<ScriptedNetworkTransport>());
        provider.GetServices<INetworkTransport>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddAgentNetworkInMemory_WhenGrantStoreMissing_FailsClosedAtResolution()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentNetworkInMemory();
        using var provider = services.BuildServiceProvider();
        var action = () => provider.GetRequiredService<INetworkTransport>();
        _ = action.ShouldThrow<InvalidOperationException>();
    }

    private static NetworkDestination Destination() => new("https", new NormalizedHost("example.test"), 443, NetworkRoute.Root);
    private static NetworkBounds Bounds() => new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 1_024, 3);
    private static NetworkAddress Address() => new(IPAddress.Parse("192.0.2.1"), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1));
    private static NetworkResolutionRequest ResolutionRequest() => new(new NetworkOperationId(Guid.Parse("50000000-0000-0000-0000-000000000005")), Destination(), Bounds(), TestSecurity.Grant());
}
