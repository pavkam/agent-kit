// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;



/// <summary>Verifies DefaultNetworkTransport behavior and contracts.</summary>
public sealed class DefaultNetworkTransportTests
{
    [Fact]
    public async Task SendAsync_WhenGrantRejected_PerformsNoConnectionAndUsesExactEvidence()
    {
        var store = new TestGrantStore
        {
            Status = GrantConsumptionStatus.Expired
        };
        using var transport = Transport(store);
        var request = Request(Destination(1));
        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<NetworkDenied>();
        var enforcement = store.Enforcements.ShouldHaveSingleItem();
        enforcement.Audience.ShouldBe(transport.SecurityAudience);
        enforcement.Resources.ShouldBe(NetworkSecurityBinding.RequestResources(request));
        enforcement.InputFingerprint.ShouldBe(NetworkSecurityBinding.RequestFingerprint(request));
    }

    [Fact]
    public async Task SendAsync_WhenStoreReconcilesAnEarlierIntent_DeniesBeforeConnection()
    {
        var store = new TestGrantStore
        {
            Status = GrantConsumptionStatus.Reconciled
        };
        await using var server = LoopbackServer.Start("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var transport = Transport(store);
        var result = await transport.SendAsync(Request(Destination(server.Port)), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<NetworkDenied>();
        _ = store.Intents.ShouldHaveSingleItem();
        server.AcceptedConnections.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_WhenCallerAlreadyCancelled_DoesNotConsumeOrConnect()
    {
        var store = new TestGrantStore();
        using var transport = Transport(store);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var action = async () => await transport.SendAsync(Request(Destination(1)), cancellation.Token);
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        store.Enforcements.ShouldBeEmpty();
        store.Intents.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenCallerCancelsDuringNonCooperativeConsumption_DoesNotConnect()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new TestGrantStore
        {
            OnIntentConsumption = cancellation.Cancel
        };
        await using var server = LoopbackServer.Start("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var transport = Transport(store);
        var action = async () => await transport.SendAsync(Request(Destination(server.Port)), cancellation.Token);
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        _ = store.Intents.ShouldHaveSingleItem();
        server.AcceptedConnections.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_WhenResponseAuthorized_ReturnsBoundedOwnedBody()
    {
        await using var server = LoopbackServer.Start("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello");
        using var transport = Transport(new TestGrantStore());
        var result = await transport.SendAsync(Request(Destination(server.Port)), TestContext.Current.CancellationToken);
        var received = result.ShouldBeOfType<NetworkResponseReceived>();
        using var reader = new StreamReader(received.Response.Content);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe("hello");
        await received.Response.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationEvidence()
    {
        await using var server = LoopbackServer.Start("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        var clock = new FixedTimeProvider();
        var store = new InMemorySecurityGrantStore(clock);
        using var transport = new DefaultNetworkTransport(store, clock, Options.Create(OptionsForNetwork()));
        var request = Request(Destination(server.Port));
        var grant = TestSecurity.CapturedGrant(transport.SecurityAudience, NetworkSecurityBinding.RequestResources(request), NetworkSecurityBinding.RequestFingerprint(request));
        request = new NetworkRequest(request.Id, request.Method, request.Destination, request.Headers, request.Content, request.Bounds, request.ResolvedAddresses, request.Classification, grant);
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        var received = result.ShouldBeOfType<NetworkResponseReceived>();
        await received.Response.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_WhenActualStreamExceedsMaximum_ThrowsTypedBoundaryException()
    {
        await using var server = LoopbackServer.Start("HTTP/1.1 200 OK\r\nConnection: close\r\n\r\ntoo-large");
        using var transport = Transport(new TestGrantStore());
        var request = Request(Destination(server.Port), Bounds(maximumResponseBytes: 4));
        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        var received = result.ShouldBeOfType<NetworkResponseReceived>();
        var action = async () => await received.Response.Content.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrowAsync<NetworkResponseTooLargeException>();
        exception.MaximumBytes.ShouldBe(4);
        exception.ObservedBytes.ShouldBeGreaterThan(4);
        await received.Response.DisposeAsync();
    }

    [Fact]
    public async Task ResponseContent_WhenDeadlineAdvancesAfterHeaders_ThrowsTypedTimeoutBeforeRead()
    {
        await using var server = LoopbackServer.Start("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello");
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        using var transport = Transport(new TestGrantStore(), time);
        var request = Request(Destination(server.Port), Bounds(responseTimeout: TimeSpan.FromSeconds(2)));
        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        var received = result.ShouldBeOfType<NetworkResponseReceived>();
        time.Advance(TimeSpan.FromSeconds(3));
        var action = async () => await received.Response.Content.ReadAsync(new byte[1], TestContext.Current.CancellationToken);
        _ = await action.ShouldThrowAsync<NetworkResponseTimedOutException>();
        await received.Response.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_WhenRedirectReceived_DoesNotFollowWithoutFreshAuthority()
    {
        await using var server = LoopbackServer.Start("HTTP/1.1 302 Found\r\nLocation: https://example.com/next\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var transport = Transport(new TestGrantStore());
        var result = await transport.SendAsync(Request(Destination(server.Port)), TestContext.Current.CancellationToken);
        var redirect = result.ShouldBeOfType<NetworkRedirectReceived>();
        redirect.Destination.Host.ShouldBe(new NormalizedHost("example.com"));
        redirect.CrossOrigin.ShouldBeTrue();
    }

    private static DefaultNetworkTransport Transport(TestGrantStore store, TimeProvider? timeProvider = null) => new(store, timeProvider ?? new FixedTimeProvider(), Options.Create(OptionsForNetwork()));
    private static AgentNetworkOptions OptionsForNetwork() => new()
    {
        DestinationPolicy = new NetworkDestinationPolicy(["http", "https"], null, allowPrivateAddresses: true),
        AddressResolutionLifetime = TimeSpan.FromMinutes(1),
    };
    private static NetworkDestination Destination(int port) => new("http", new NormalizedHost("127.0.0.1"), port, NetworkRoute.Root);
    private static NetworkBounds Bounds(TimeSpan? responseTimeout = null, long maximumResponseBytes = 1_024) => new(TimeSpan.FromSeconds(2), responseTimeout ?? TimeSpan.FromSeconds(2), maximumResponseBytes, 3);
    private static NetworkAddress Address() => new(IPAddress.Loopback, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1));
    private static NetworkRequest Request(NetworkDestination destination, NetworkBounds? bounds = null) => new(new NetworkOperationId(Guid.Parse("70000000-0000-0000-0000-000000000007")), NetworkMethod.Get, destination, NetworkHeaderSet.Empty, null, bounds ?? Bounds(), [Address()], NetworkDataClassification.Public, TestSecurity.Grant());
    [Fact]
    public void Constructors_WhenIntentIdsNull_ThrowWithExactParameterName()
    {
        var store = new TestGrantStore();
        var transport = Should.Throw<ArgumentNullException>(() => new DefaultNetworkTransport(store, new FixedTimeProvider(), Options.Create(OptionsForNetwork()), null, null!));
        transport.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public void Constructor_WhenLegacyLoggerArgumentIsNull_RetainsUnambiguousSourceCompatibility()
    {
        using var transport = new DefaultNetworkTransport(new TestGrantStore(), new FixedTimeProvider(), Options.Create(OptionsForNetwork()), null);
    }
}
