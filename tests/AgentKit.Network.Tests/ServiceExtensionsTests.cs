// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentNetwork_WhenCalled_RegistersDefaultResolverAndTransport()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentNetwork();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<INetworkNameResolver>().ShouldBeOfType<DefaultNetworkNameResolver>();
        _ = provider.GetRequiredService<INetworkTransport>().ShouldBeOfType<DefaultNetworkTransport>();
    }

    [Fact]
    public void AddAgentNetwork_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentNetwork();
        _ = services.AddAgentNetwork();

        using var provider = services.BuildServiceProvider();
        provider.GetServices<INetworkNameResolver>().Count().ShouldBe(1);
        provider.GetServices<INetworkTransport>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddAgentNetwork_WhenAddressResolutionLifetimeIsNotPositive_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentNetwork(static options => options.AddressResolutionLifetime = TimeSpan.Zero);

        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(provider.GetRequiredService<INetworkNameResolver>);
    }

    [Fact]
    public void AddAgentNetwork_AppliesConfiguredDestinationPolicy()
    {
        var services = new ServiceCollection();
        var policy = new NetworkDestinationPolicy(["http"], null, allowPrivateAddresses: true);

        _ = services.AddAgentNetwork(options => options.DestinationPolicy = policy);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<AgentNetworkOptions>>().Value.DestinationPolicy.ShouldBe(policy);
    }
}
