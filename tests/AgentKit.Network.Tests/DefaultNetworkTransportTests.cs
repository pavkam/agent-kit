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
        received.Response.Metadata.StatusCode.ShouldBe(200);
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
    public async Task SendAsync_WhenDeclaredContentLengthExceedsMaximum_ReturnsResponseLimitExceededWithoutReading()
    {
        await using var server = LoopbackServer.Start("HTTP/1.1 200 OK\r\nContent-Length: 9\r\nConnection: close\r\n\r\ntoo-large");
        using var transport = Transport(new TestGrantStore());
        var request = Request(Destination(server.Port), Bounds(maximumResponseBytes: 4));
        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        var limitExceeded = result.ShouldBeOfType<NetworkResponseLimitExceeded>();
        limitExceeded.ObservedBytes.ShouldBe(9);
        limitExceeded.MaximumBytes.ShouldBe(4);
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
        redirect.Destination.Port.ShouldBe(443);
        redirect.CrossOrigin.ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_WhenRedirectLocationIsRelative_ResolvesAgainstTheOriginalDestination()
    {
        await using var server = LoopbackServer.Start("HTTP/1.1 302 Found\r\nLocation: /next\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var transport = Transport(new TestGrantStore());
        var result = await transport.SendAsync(Request(Destination(server.Port)), TestContext.Current.CancellationToken);
        var redirect = result.ShouldBeOfType<NetworkRedirectReceived>();
        redirect.Destination.Host.ShouldBe(new NormalizedHost("127.0.0.1"));
        redirect.Destination.Port.ShouldBe(server.Port);
        redirect.Destination.Route.Value.ShouldBe("/next");
        redirect.CrossOrigin.ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_WhenRedirectLocationHasAnExplicitNonDefaultPort_PreservesThatPort()
    {
        await using var server = LoopbackServer.Start("HTTP/1.1 302 Found\r\nLocation: http://example.com:8080/next\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var transport = Transport(new TestGrantStore());
        var result = await transport.SendAsync(Request(Destination(server.Port)), TestContext.Current.CancellationToken);
        var redirect = result.ShouldBeOfType<NetworkRedirectReceived>();
        redirect.Destination.Host.ShouldBe(new NormalizedHost("example.com"));
        redirect.Destination.Port.ShouldBe(8080);
        redirect.CrossOrigin.ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_WhenSchemeIsExcludedByPolicy_ReturnsDeniedWithoutConnecting()
    {
        await using var server = LoopbackServer.Start("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        var store = new TestGrantStore();
        var options = new AgentNetworkOptions
        {
            DestinationPolicy = new NetworkDestinationPolicy(["https"], null, allowPrivateAddresses: true),
            AddressResolutionLifetime = TimeSpan.FromMinutes(1),
        };
        using var transport = new DefaultNetworkTransport(store, new FixedTimeProvider(), Options.Create(options));
        var result = await transport.SendAsync(Request(Destination(server.Port)), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<NetworkDenied>().SafeMessage.ShouldContain("excluded by the configured network policy");
        server.AcceptedConnections.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_WhenNoResolvedAddressIsStillValid_ReturnsDeniedWithoutConnecting()
    {
        await using var server = LoopbackServer.Start("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var transport = Transport(new TestGrantStore());
        var expired = new NetworkAddress(IPAddress.Loopback, DateTimeOffset.UnixEpoch.AddMinutes(-2), DateTimeOffset.UnixEpoch.AddMinutes(-1));
        var baseline = Request(Destination(server.Port));
        var request = new NetworkRequest(baseline.Id, baseline.Method, baseline.Destination, baseline.Headers, baseline.Content, baseline.Bounds, [expired], baseline.Classification, baseline.Grant);
        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        result.ShouldBeOfType<NetworkDenied>().SafeMessage.ShouldContain("No still-valid resolved address");
        server.AcceptedConnections.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_WhenSecondSendReusesAKeepAliveEligiblePooledConnection_OpensAFreshConnectionInstead()
    {
        // A response without "Connection: close" is keep-alive eligible under HTTP/1.1, so
        // SocketsHttpHandler's default pool would normally return this connection for reuse by a
        // later, unrelated send to the same (scheme, host, port). Each send here carries its own
        // pinned resolved address and must be independently verified via ConnectCallback, which is
        // only invoked for a genuinely new connection - so if the fix holds, both sends open their
        // own connection instead of the second one silently reusing the first's.
        await using var server = LoopbackServer.StartKeepAlive("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n", maxConnections: 2);
        using var transport = Transport(new TestGrantStore());
        var request = Request(Destination(server.Port));

        var first = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        var firstReceived = first.ShouldBeOfType<NetworkResponseReceived>();
        await firstReceived.Response.DisposeAsync();
        var second = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        var secondReceived = second.ShouldBeOfType<NetworkResponseReceived>();
        await secondReceived.Response.DisposeAsync();

        server.AcceptedConnections.ShouldBe(2, "a pooled connection reused across independently authorized sends would bypass per-send address verification");
    }

    [Fact]
    public async Task SendAsync_WhenConnectionIsRefused_ReturnsConnectionFailedWithCertainSideEffect()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint) probe.LocalEndpoint).Port;
        probe.Stop();
        using var transport = Transport(new TestGrantStore());
        var result = await transport.SendAsync(Request(Destination(port)), TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<NetworkRequestFailed>();
        failed.Kind.ShouldBe(NetworkFailureKind.ConnectionFailed);
        failed.SideEffectCertain.ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_WhenServerSendsAMalformedResponse_ReturnsConnectionFailedWithoutCertainSideEffect()
    {
        await using var server = LoopbackServer.Start("not a valid HTTP response at all\r\n\r\n");
        using var transport = Transport(new TestGrantStore());
        var result = await transport.SendAsync(Request(Destination(server.Port)), TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<NetworkRequestFailed>();
        failed.Kind.ShouldBe(NetworkFailureKind.ConnectionFailed);
        failed.SideEffectCertain.ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_WhenResponseDeadlineElapsesBeforeAnyResponse_ReturnsTimedOutWithoutCertainSideEffect()
    {
        await using var server = LoopbackServer.StartHanging();
        using var transport = Transport(new TestGrantStore());
        var request = Request(Destination(server.Port), Bounds(responseTimeout: TimeSpan.FromMilliseconds(200)));
        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<NetworkRequestFailed>();
        failed.Kind.ShouldBe(NetworkFailureKind.Timeout);
        failed.SideEffectCertain.ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_WhenCallerCancelsWhileWaitingForResponse_ReturnsCancelledWithoutCertainSideEffect()
    {
        await using var server = LoopbackServer.StartHanging();
        using var transport = Transport(new TestGrantStore());
        using var cancellation = new CancellationTokenSource();
        var request = Request(Destination(server.Port), Bounds(responseTimeout: TimeSpan.FromSeconds(30)));
        var sendTask = transport.SendAsync(request, cancellation.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        var result = await sendTask;
        var cancelled = result.ShouldBeOfType<NetworkCancelled>();
        cancelled.SideEffectCertain.ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_WhenRequestHasHeadersAndContent_SendsThemToTheServer()
    {
        await using var server = LoopbackServer.Start("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var transport = Transport(new TestGrantStore());
        var content = new NetworkRequestContent("text/plain", "payload"u8.ToArray());
        var headers = new NetworkHeaderSet([new NetworkHeader("X-Test", "value")]);
        var baseline = Request(Destination(server.Port));
        var request = new NetworkRequest(baseline.Id, baseline.Method, baseline.Destination, headers, content, baseline.Bounds, baseline.ResolvedAddresses, baseline.Classification, baseline.Grant);
        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        var received = result.ShouldBeOfType<NetworkResponseReceived>();
        received.Response.Metadata.StatusCode.ShouldBe(200);
        await received.Response.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_WhenActionThrowsUnexpectedException_PropagatesAfterObservingFailure()
    {
        var store = new TestGrantStore { OnIntentConsumption = static () => throw new InvalidOperationException("boom") };
        using var transport = Transport(store);
        var action = async () => await transport.SendAsync(Request(Destination(1)), TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldBe("boom");
    }

    [Fact]
    public async Task SendAsync_WhenLoggerIsEnabledAndResponseAuthorized_EmitsCompletedStructuredEvent()
    {
        await using var server = LoopbackServer.Start("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        var logger = new RecordingLogger<DefaultNetworkTransport>();
        using var transport = Transport(new TestGrantStore(), logger: logger);
        var request = Request(Destination(server.Port));
        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        var received = result.ShouldBeOfType<NetworkResponseReceived>();
        await received.Response.DisposeAsync();
        var completed = logger.Snapshot().ShouldHaveSingleItem();
        completed.EventId.Id.ShouldBe(14000);
        completed.State["Stage"].ShouldBe("send");
        completed.State["NetworkOperationId"].ShouldBe(request.Id);
    }

    [Fact]
    public async Task SendAsync_WhenLoggerIsEnabledAndActionThrowsUnexpectedException_EmitsFailedStructuredEvent()
    {
        var logger = new RecordingLogger<DefaultNetworkTransport>();
        var store = new TestGrantStore { OnIntentConsumption = static () => throw new InvalidOperationException("boom") };
        using var transport = Transport(store, logger: logger);
        var request = Request(Destination(1));
        var action = async () => await transport.SendAsync(request, TestContext.Current.CancellationToken);
        _ = await action.ShouldThrowAsync<InvalidOperationException>();
        var failed = logger.Snapshot().ShouldHaveSingleItem();
        failed.EventId.Id.ShouldBe(14001);
        failed.State["Stage"].ShouldBe("send");
        failed.State["NetworkOperationId"].ShouldBe(request.Id);
        failed.State["ErrorType"].ShouldBe(typeof(InvalidOperationException).FullName);
    }

    private static DefaultNetworkTransport Transport(TestGrantStore store, TimeProvider? timeProvider = null, ILogger<DefaultNetworkTransport>? logger = null) => new(store, timeProvider ?? new FixedTimeProvider(), Options.Create(OptionsForNetwork()), logger);
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
