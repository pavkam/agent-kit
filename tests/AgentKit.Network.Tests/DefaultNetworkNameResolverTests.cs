// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;

public sealed class DefaultNetworkNameResolverTests
{
    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DefaultNetworkNameResolver(null!, Options.Create(new AgentNetworkOptions())));

        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public void Constructor_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DefaultNetworkNameResolver(TimeProvider.System, null!));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task ResolveAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var resolver = TestFactory.Resolver();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => resolver.ResolveAsync(null!, TestContext.Current.CancellationToken).AsTask());

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task ResolveAsync_WhenSchemeNotAllowed_ReturnsResolutionDenied()
    {
        var policy = new NetworkDestinationPolicy(["https"], null, allowPrivateAddresses: true);
        var resolver = TestFactory.Resolver(policy);
        var request = TestFactory.ResolutionRequest(TestFactory.LoopbackDestination(80));

        var result = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<NetworkResolutionDenied>();
    }

    [Fact]
    public async Task ResolveAsync_WhenHostIsIpLiteralAndAllowed_ResolvesWithoutDns()
    {
        var resolver = TestFactory.Resolver();
        var request = TestFactory.ResolutionRequest(TestFactory.LoopbackDestination(80));

        var result = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        var resolved = result.ShouldBeOfType<NetworkResolved>();
        resolved.Addresses.Length.ShouldBe(1);
        resolved.Addresses[0].Address.ShouldBe(IPAddress.Loopback);
    }

    [Fact]
    public async Task ResolveAsync_WhenIpLiteralIsPrivateAndPolicyDisallowsPrivateAddresses_ReturnsResolutionDenied()
    {
        var policy = new NetworkDestinationPolicy(["http"], null, allowPrivateAddresses: false);
        var resolver = TestFactory.Resolver(policy);
        var request = TestFactory.ResolutionRequest(TestFactory.LoopbackDestination(80));

        var result = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<NetworkResolutionDenied>();
    }

    [Fact]
    public async Task ResolveAsync_WhenHostIsLocalhostName_ResolvesToLoopbackAddress()
    {
        var resolver = TestFactory.Resolver();
        var destination = new NetworkDestination("http", new NormalizedHost("localhost"), 80, NetworkRoute.Root);
        var request = TestFactory.ResolutionRequest(destination);

        var result = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        var resolved = result.ShouldBeOfType<NetworkResolved>();
        resolved.Addresses.ShouldNotBeEmpty();
        resolved.Addresses.ShouldAllBe(static address => IPAddress.IsLoopback(address.Address));
    }

    [Fact]
    public async Task ResolveAsync_SetsAddressExpiryUsingConfiguredLifetimeAndInjectedTimeProvider()
    {
        var timeProvider = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var resolver = new DefaultNetworkNameResolver(
            timeProvider,
            Options.Create(new AgentNetworkOptions
            {
                DestinationPolicy = TestFactory.AllowLoopbackHttpPolicy(),
                AddressResolutionLifetime = TimeSpan.FromMinutes(2),
            }));
        var request = TestFactory.ResolutionRequest(TestFactory.LoopbackDestination(80));

        var result = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        var resolved = result.ShouldBeOfType<NetworkResolved>();
        resolved.Addresses[0].ResolvedAt.ShouldBe(timeProvider.GetUtcNow());
        resolved.Addresses[0].ExpiresAt.ShouldBe(timeProvider.GetUtcNow().AddMinutes(2));
    }
}
