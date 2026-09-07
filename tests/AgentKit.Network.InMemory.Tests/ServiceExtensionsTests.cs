// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddInMemoryNetwork_WhenCalled_RegistersScriptedResolverAndTransport()
    {
        var services = new ServiceCollection();

        _ = services.AddInMemoryNetwork();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<INetworkNameResolver>().ShouldBeOfType<ScriptedNetworkNameResolver>();
        _ = provider.GetRequiredService<INetworkTransport>().ShouldBeOfType<ScriptedNetworkTransport>();
    }

    [Fact]
    public void AddInMemoryNetwork_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.AddInMemoryNetwork();
        _ = services.AddInMemoryNetwork();

        using var provider = services.BuildServiceProvider();
        provider.GetServices<INetworkNameResolver>().Count().ShouldBe(1);
        provider.GetServices<INetworkTransport>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddInMemoryNetwork_ResolverAndTransportShareRegisteredInstanceAcrossInterfaceAndConcreteResolution()
    {
        var services = new ServiceCollection();

        _ = services.AddInMemoryNetwork();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<INetworkNameResolver>()
            .ShouldBeSameAs(provider.GetRequiredService<ScriptedNetworkNameResolver>());
        provider.GetRequiredService<INetworkTransport>()
            .ShouldBeSameAs(provider.GetRequiredService<ScriptedNetworkTransport>());
    }
}
