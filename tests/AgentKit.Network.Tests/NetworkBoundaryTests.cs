// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;

public sealed class NetworkBoundaryTests
{
    [Fact]
    public void Constructors_WhenLegacyLoggerArgumentIsNull_RetainUnambiguousSourceCompatibility()
    {
        var store = new TestGrantStore();

        _ = new DefaultNetworkNameResolver(store, new FixedTimeProvider(), Options.Create(OptionsForNetwork()), null);
        using var transport = new DefaultNetworkTransport(store, new FixedTimeProvider(), Options.Create(OptionsForNetwork()), null);
    }

    [Fact]
    public void Constructors_WhenIntentIdsNull_ThrowWithExactParameterName()
    {
        var store = new TestGrantStore();

        var resolver = Should.Throw<ArgumentNullException>(() => new DefaultNetworkNameResolver(
            store, new FixedTimeProvider(), Options.Create(OptionsForNetwork()), null, null!));
        var transport = Should.Throw<ArgumentNullException>(() => new DefaultNetworkTransport(
            store, new FixedTimeProvider(), Options.Create(OptionsForNetwork()), null, null!));

        resolver.ParamName.ShouldBe("intentIds");
        transport.ParamName.ShouldBe("intentIds");
    }

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
    public async Task ResolveAsync_WhenStoreReconcilesAnEarlierIntent_DeniesBeforeResolution()
    {
        var store = new TestGrantStore { Status = GrantConsumptionStatus.Reconciled };
        var resolver = Resolver(store);

        var result = await resolver.ResolveAsync(ResolutionRequest(Destination(443)), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<NetworkResolutionDenied>();
        _ = store.Intents.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ResolveAsync_WhenConsumedResultLacksExactReceipt_DeniesBeforeResolution()
    {
        var store = new TestGrantStore { IncludeIntentReceipt = false };
        var resolver = Resolver(store);

        var result = await resolver.ResolveAsync(ResolutionRequest(Destination(443)), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<NetworkResolutionDenied>().SafeMessage.ShouldContain("enforcement-intent receipt");
    }

    [Fact]
    public async Task ResolveAsync_WhenReceiptIdentityDiffers_DeniesBeforeResolution()
    {
        var store = new TestGrantStore { ReturnExactIntentReceipt = false };
        var resolver = Resolver(store);

        var result = await resolver.ResolveAsync(ResolutionRequest(Destination(443)), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<NetworkResolutionDenied>();
    }

    [Fact]
    public async Task ResolveAsync_WhenCallerAlreadyCancelled_DoesNotConsumeOrResolve()
    {
        var store = new TestGrantStore();
        var resolver = Resolver(store);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = async () => await resolver.ResolveAsync(ResolutionRequest(Destination(443)), cancellation.Token);

        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        store.Enforcements.ShouldBeEmpty();
        store.Intents.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WhenReceiptAccepted_UsesInjectedFreshIntentId()
    {
        var store = new TestGrantStore();
        var expectedId = new SecurityEnforcementIntentId(Guid.Parse("80000000-0000-0000-0000-000000000008"));
        var resolver = Resolver(store, new SequenceSecurityEnforcementIntentIdGenerator(expectedId.Value));

        _ = await resolver.ResolveAsync(ResolutionRequest(Destination(443)), TestContext.Current.CancellationToken);

        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(expectedId);
        store.LegacyConsumptionCalls.ShouldBe(0);
    }

    [Fact]
    public async Task AddAgentNetwork_WhenIntentGeneratorIsHostSupplied_UsesTheReplacement()
    {
        var store = new TestGrantStore();
        var expectedId = new SecurityEnforcementIntentId(Guid.Parse("81000000-0000-0000-0000-000000000008"));
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(
            new SequenceSecurityEnforcementIntentIdGenerator(expectedId.Value));
        _ = services.AddAgentNetwork(value =>
            value.DestinationPolicy = new NetworkDestinationPolicy(["http"], null, allowPrivateAddresses: true));
        using var provider = services.BuildServiceProvider();

        _ = await provider.GetRequiredService<INetworkNameResolver>().ResolveAsync(
            ResolutionRequest(Destination(443)),
            TestContext.Current.CancellationToken);

        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(expectedId);
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
    public async Task ResolveAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationEvidence()
    {
        var clock = new FixedTimeProvider();
        var store = new InMemorySecurityGrantStore(clock);
        var resolver = new DefaultNetworkNameResolver(store, clock, Options.Create(OptionsForNetwork()));
        var request = ResolutionRequest(Destination(443));
        var grant = TestSecurity.CapturedGrant(
            resolver.SecurityAudience,
            [NetworkSecurityBinding.ResolutionResource(request.Destination)],
            NetworkSecurityBinding.ResolutionFingerprint(request));
        request = new NetworkResolutionRequest(request.Id, request.Destination, request.Bounds, grant);
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);

        var result = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<NetworkResolved>();
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
    public async Task SendAsync_WhenStoreReconcilesAnEarlierIntent_DeniesBeforeConnection()
    {
        var store = new TestGrantStore { Status = GrantConsumptionStatus.Reconciled };
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
        var store = new TestGrantStore { OnIntentConsumption = cancellation.Cancel };
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
        var grant = TestSecurity.CapturedGrant(
            transport.SecurityAudience,
            NetworkSecurityBinding.RequestResources(request),
            NetworkSecurityBinding.RequestFingerprint(request));
        request = new NetworkRequest(
            request.Id,
            request.Method,
            request.Destination,
            request.Headers,
            request.Content,
            request.Bounds,
            request.ResolvedAddresses,
            request.Classification,
            grant);
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);

        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);

        var received = result.ShouldBeOfType<NetworkResponseReceived>();
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

    private static DefaultNetworkNameResolver Resolver(
        TestGrantStore store,
        IIdentifierGenerator<SecurityEnforcementIntentId>? intentIds = null) => intentIds is null
        ? new DefaultNetworkNameResolver(store, new FixedTimeProvider(), Options.Create(OptionsForNetwork()))
        : new DefaultNetworkNameResolver(store, new FixedTimeProvider(), Options.Create(OptionsForNetwork()), null, intentIds);

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
