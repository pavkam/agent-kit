// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;

public sealed class NetworkBoundaryTests
{
    [Fact]
    public async Task ResolveAsync_WhenObserved_EmitsContentFreeOperationActivity()
    {
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var resolver = Resolver(new TestGrantStore { Status = GrantConsumptionStatus.Mismatch });
        var request = ResolutionRequest(Destination(443));

        _ = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.NetworkResolve);
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.NetworkOperationId).ShouldBe(request.Id.ToString());
        string.Join('|', activity.TagObjects.Select(static tag => $"{tag.Key}={tag.Value}"))
            .ShouldNotContain("127.0.0.1");
    }

    [Fact]
    public async Task ResolveAsync_WhenGrantRejected_PerformsNoResolutionAndReturnsDenied()
    {
        var store = new TestGrantStore { Status = GrantConsumptionStatus.Mismatch };
        var resolver = Resolver(store);
        var request = ResolutionRequest(Destination(443));

        var result = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<NetworkResolutionDenied>();
        var enforcement = store.Enforcements.ShouldHaveSingleItem();
        enforcement.Audience.ShouldBe(resolver.SecurityAudience);
        enforcement.Resources.ShouldBe([NetworkSecurityBinding.ResolutionResource(request.Destination)]);
        enforcement.InputFingerprint.ShouldBe(NetworkSecurityBinding.ResolutionFingerprint(request));
    }

    [Fact]
    public async Task ResolveAsync_WhenIpLiteralAuthorized_ReturnsFreshPolicyEligibleAddress()
    {
        var resolver = Resolver(new TestGrantStore());

        var result = await resolver.ResolveAsync(
            ResolutionRequest(Destination(443)),
            TestContext.Current.CancellationToken);

        var resolved = result.ShouldBeOfType<NetworkResolved>();
        resolved.Addresses.ShouldHaveSingleItem().Address.ShouldBe(IPAddress.Loopback);
        resolved.Addresses[0].ExpiresAt.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(1));
    }

    [Fact]
    public async Task SendAsync_WhenGrantRejected_PerformsNoConnectionAndUsesExactEvidence()
    {
        var store = new TestGrantStore { Status = GrantConsumptionStatus.Expired };
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
    public async Task SendAsync_WhenActualStreamExceedsMaximum_ThrowsTypedBoundaryException()
    {
        await using var server = LoopbackServer.Start(
            "HTTP/1.1 200 OK\r\nConnection: close\r\n\r\ntoo-large");
        using var transport = Transport(new TestGrantStore());
        var request = Request(Destination(server.Port), Bounds(maximumResponseBytes: 4));

        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);

        var received = result.ShouldBeOfType<NetworkResponseReceived>();
        var action = async () => await received.Response.Content.CopyToAsync(
            Stream.Null,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrowAsync<NetworkResponseTooLargeException>();
        exception.MaximumBytes.ShouldBe(4);
        exception.ObservedBytes.ShouldBeGreaterThan(4);
        await received.Response.DisposeAsync();
    }

    [Fact]
    public async Task ResponseContent_WhenDeadlineAdvancesAfterHeaders_ThrowsTypedTimeoutBeforeRead()
    {
        await using var server = LoopbackServer.Start(
            "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello");
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        using var transport = Transport(new TestGrantStore(), time);
        var request = Request(Destination(server.Port), Bounds(responseTimeout: TimeSpan.FromSeconds(2)));
        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        var received = result.ShouldBeOfType<NetworkResponseReceived>();

        time.Advance(TimeSpan.FromSeconds(3));
        var action = async () => await received.Response.Content.ReadAsync(
            new byte[1],
            TestContext.Current.CancellationToken);

        _ = await action.ShouldThrowAsync<NetworkResponseTimedOutException>();
        await received.Response.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_WhenRedirectReceived_DoesNotFollowWithoutFreshAuthority()
    {
        await using var server = LoopbackServer.Start(
            "HTTP/1.1 302 Found\r\nLocation: https://example.com/next\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var transport = Transport(new TestGrantStore());

        var result = await transport.SendAsync(Request(Destination(server.Port)), TestContext.Current.CancellationToken);

        var redirect = result.ShouldBeOfType<NetworkRedirectReceived>();
        redirect.Destination.Host.ShouldBe(new NormalizedHost("example.com"));
        redirect.CrossOrigin.ShouldBeTrue();
    }

    [Fact]
    public void RequestFingerprint_WhenHeadersAndBodySensitive_ContainsNoRawValues()
    {
        var request = new NetworkRequest(
            new NetworkOperationId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            NetworkMethod.Post,
            Destination(443),
            new NetworkHeaderSet([new NetworkHeader("Authorization", "Bearer secret")]),
            new NetworkRequestContent("text/plain", "private body"u8.ToArray()),
            Bounds(),
            [Address()],
            NetworkDataClassification.Confidential,
            TestSecurity.Grant());

        var fingerprint = NetworkSecurityBinding.RequestFingerprint(request);

        fingerprint.Value.ShouldStartWith("sha256:");
        fingerprint.Value.ShouldNotContain("secret");
        fingerprint.Value.ShouldNotContain("private body");
    }

    private static DefaultNetworkNameResolver Resolver(TestGrantStore store) => new(
        store,
        new FixedTimeProvider(),
        Options.Create(OptionsForNetwork()));

    private static DefaultNetworkTransport Transport(TestGrantStore store, TimeProvider? timeProvider = null) => new(
        store,
        timeProvider ?? new FixedTimeProvider(),
        Options.Create(OptionsForNetwork()));

    private static AgentNetworkOptions OptionsForNetwork() => new()
    {
        DestinationPolicy = new NetworkDestinationPolicy(["http", "https"], null, allowPrivateAddresses: true),
        AddressResolutionLifetime = TimeSpan.FromMinutes(1),
    };

    private static NetworkDestination Destination(int port) => new(
        "http",
        new NormalizedHost("127.0.0.1"),
        port,
        NetworkRoute.Root);

    private static NetworkBounds Bounds(
        TimeSpan? responseTimeout = null,
        long maximumResponseBytes = 1_024) => new(
        TimeSpan.FromSeconds(2),
        responseTimeout ?? TimeSpan.FromSeconds(2),
        maximumResponseBytes,
        3);

    private static NetworkAddress Address() => new(
        IPAddress.Loopback,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch.AddMinutes(1));

    private static NetworkResolutionRequest ResolutionRequest(NetworkDestination destination) => new(
        new NetworkOperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
        destination,
        Bounds(),
        TestSecurity.Grant());

    private static NetworkRequest Request(NetworkDestination destination, NetworkBounds? bounds = null) => new(
        new NetworkOperationId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
        NetworkMethod.Get,
        destination,
        NetworkHeaderSet.Empty,
        null,
        bounds ?? Bounds(),
        [Address()],
        NetworkDataClassification.Public,
        TestSecurity.Grant());

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;
}
