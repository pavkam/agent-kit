// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;



/// <summary>Verifies DefaultNetworkNameResolver behavior and contracts.</summary>
public sealed class DefaultNetworkNameResolverTests
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
        string.Join('|', activity.TagObjects.Select(static tag => $"{tag.Key}={tag.Value}")).ShouldNotContain("127.0.0.1");
    }

    [Fact]
    public async Task ResolveAsync_WhenGrantRejected_PerformsNoResolutionAndReturnsDenied()
    {
        var store = new TestGrantStore
        {
            Status = GrantConsumptionStatus.Mismatch
        };
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
        var store = new TestGrantStore
        {
            Status = GrantConsumptionStatus.Reconciled
        };
        var resolver = Resolver(store);
        var result = await resolver.ResolveAsync(ResolutionRequest(Destination(443)), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<NetworkResolutionDenied>();
        _ = store.Intents.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ResolveAsync_WhenConsumedResultLacksExactReceipt_DeniesBeforeResolution()
    {
        var store = new TestGrantStore
        {
            IncludeIntentReceipt = false
        };
        var resolver = Resolver(store);
        var result = await resolver.ResolveAsync(ResolutionRequest(Destination(443)), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<NetworkResolutionDenied>().SafeMessage.ShouldContain("enforcement-intent receipt");
    }

    [Fact]
    public async Task ResolveAsync_WhenReceiptIdentityDiffers_DeniesBeforeResolution()
    {
        var store = new TestGrantStore
        {
            ReturnExactIntentReceipt = false
        };
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
    public async Task ResolveAsync_WhenIpLiteralAuthorized_ReturnsFreshPolicyEligibleAddress()
    {
        var resolver = Resolver(new TestGrantStore());
        var result = await resolver.ResolveAsync(ResolutionRequest(Destination(443)), TestContext.Current.CancellationToken);
        var resolved = result.ShouldBeOfType<NetworkResolved>();
        resolved.Addresses.ShouldHaveSingleItem().Address.ShouldBe(IPAddress.Loopback);
        resolved.Addresses[0].ExpiresAt.ShouldBe(DateTimeOffset.UnixEpoch.AddMinutes(1));
    }

    [Fact]
    public async Task ResolveAsync_WhenSchemeIsExcludedByPolicy_ReturnsDeniedWithoutResolving()
    {
        var store = new TestGrantStore();
        var options = new AgentNetworkOptions
        {
            DestinationPolicy = new NetworkDestinationPolicy(["https"], null, allowPrivateAddresses: true),
            AddressResolutionLifetime = TimeSpan.FromMinutes(1),
        };
        var resolver = new DefaultNetworkNameResolver(store, new FixedTimeProvider(), Options.Create(options));
        var result = await resolver.ResolveAsync(ResolutionRequest(Destination(443)), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<NetworkResolutionDenied>().SafeMessage.ShouldContain("excluded by the configured network policy");
    }

    [Fact]
    public async Task ResolveAsync_WhenHostnameCannotBeResolved_ReturnsDnsResolutionFailed()
    {
        var resolver = Resolver(new TestGrantStore());
        var destination = new NetworkDestination("http", new NormalizedHost("definitely-invalid-host-name-agentkit-test-xyz123.invalid"), 443, NetworkRoute.Root);
        var result = await resolver.ResolveAsync(ResolutionRequest(destination), TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<NetworkResolutionFailed>();
        failed.Kind.ShouldBe(NetworkFailureKind.DnsResolutionFailed);
    }

    [Fact]
    public async Task ResolveAsync_WhenAllResolvedAddressesAreExcludedByPolicy_ReturnsDenied()
    {
        var options = new AgentNetworkOptions
        {
            DestinationPolicy = new NetworkDestinationPolicy(["http", "https"], null, allowPrivateAddresses: false),
            AddressResolutionLifetime = TimeSpan.FromMinutes(1),
        };
        var resolver = new DefaultNetworkNameResolver(new TestGrantStore(), new FixedTimeProvider(), Options.Create(options));
        var result = await resolver.ResolveAsync(ResolutionRequest(Destination(443)), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<NetworkResolutionDenied>().SafeMessage.ShouldContain("No resolved address is permitted");
    }

    [Fact]
    public async Task ResolveAsync_WhenActionThrowsUnexpectedException_PropagatesAfterObservingFailure()
    {
        var store = new TestGrantStore { OnIntentConsumption = static () => throw new InvalidOperationException("boom") };
        var resolver = Resolver(store);
        var action = async () => await resolver.ResolveAsync(ResolutionRequest(Destination(443)), TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldBe("boom");
    }

    [Fact]
    public async Task ResolveAsync_WhenLoggerIsEnabledAndIpLiteralAuthorized_EmitsCompletedStructuredEvent()
    {
        var logger = new RecordingLogger<DefaultNetworkNameResolver>();
        var resolver = new DefaultNetworkNameResolver(new TestGrantStore(), new FixedTimeProvider(), Options.Create(OptionsForNetwork()), logger);
        var request = ResolutionRequest(Destination(443));
        var result = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<NetworkResolved>();
        var completed = logger.Snapshot().ShouldHaveSingleItem();
        completed.EventId.Id.ShouldBe(14000);
        completed.State["Stage"].ShouldBe("resolve");
        completed.State["NetworkOperationId"].ShouldBe(request.Id);
    }

    [Fact]
    public async Task ResolveAsync_WhenLoggerIsEnabledAndActionThrowsUnexpectedException_EmitsFailedStructuredEvent()
    {
        var logger = new RecordingLogger<DefaultNetworkNameResolver>();
        var store = new TestGrantStore { OnIntentConsumption = static () => throw new InvalidOperationException("boom") };
        var resolver = new DefaultNetworkNameResolver(store, new FixedTimeProvider(), Options.Create(OptionsForNetwork()), logger);
        var request = ResolutionRequest(Destination(443));
        var action = async () => await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);
        _ = await action.ShouldThrowAsync<InvalidOperationException>();
        var failed = logger.Snapshot().ShouldHaveSingleItem();
        failed.EventId.Id.ShouldBe(14001);
        failed.State["Stage"].ShouldBe("resolve");
        failed.State["NetworkOperationId"].ShouldBe(request.Id);
        failed.State["ErrorType"].ShouldBe(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public async Task ResolveAsync_WhenLoggerIsEnabledAndAllResolvedAddressesAreExcludedByPolicy_LogsNoEligibleAddressesWarning()
    {
        var logger = new RecordingLogger<DefaultNetworkNameResolver>();
        var options = new AgentNetworkOptions
        {
            DestinationPolicy = new NetworkDestinationPolicy(["http", "https"], null, allowPrivateAddresses: false),
            AddressResolutionLifetime = TimeSpan.FromMinutes(1),
        };
        var resolver = new DefaultNetworkNameResolver(new TestGrantStore(), new FixedTimeProvider(), Options.Create(options), logger);
        var result = await resolver.ResolveAsync(ResolutionRequest(Destination(443)), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<NetworkResolutionDenied>().SafeMessage.ShouldContain("No resolved address is permitted");
        var warning = logger.Snapshot().Single(static entry => entry.EventId.Id == 14002);
        warning.Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public async Task ResolveAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationEvidence()
    {
        var clock = new FixedTimeProvider();
        var store = new InMemorySecurityGrantStore(clock);
        var resolver = new DefaultNetworkNameResolver(store, clock, Options.Create(OptionsForNetwork()));
        var request = ResolutionRequest(Destination(443));
        var grant = TestSecurity.CapturedGrant(resolver.SecurityAudience, [NetworkSecurityBinding.ResolutionResource(request.Destination)], NetworkSecurityBinding.ResolutionFingerprint(request));
        request = new NetworkResolutionRequest(request.Id, request.Destination, request.Bounds, grant);
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        var result = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<NetworkResolved>();
    }

    private static DefaultNetworkNameResolver Resolver(TestGrantStore store, IIdentifierGenerator<SecurityEnforcementIntentId>? intentIds = null) => intentIds is null ? new DefaultNetworkNameResolver(store, new FixedTimeProvider(), Options.Create(OptionsForNetwork())) : new DefaultNetworkNameResolver(store, new FixedTimeProvider(), Options.Create(OptionsForNetwork()), null, intentIds);
    private static AgentNetworkOptions OptionsForNetwork() => new()
    {
        DestinationPolicy = new NetworkDestinationPolicy(["http", "https"], null, allowPrivateAddresses: true),
        AddressResolutionLifetime = TimeSpan.FromMinutes(1),
    };
    private static NetworkDestination Destination(int port) => new("http", new NormalizedHost("127.0.0.1"), port, NetworkRoute.Root);
    private static NetworkBounds Bounds(TimeSpan? responseTimeout = null, long maximumResponseBytes = 1_024) => new(TimeSpan.FromSeconds(2), responseTimeout ?? TimeSpan.FromSeconds(2), maximumResponseBytes, 3);
    private static NetworkResolutionRequest ResolutionRequest(NetworkDestination destination) => new(new NetworkOperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")), destination, Bounds(), TestSecurity.Grant());
    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
    [Fact]
    public void Constructors_WhenIntentIdsNull_ThrowWithExactParameterName()
    {
        var store = new TestGrantStore();
        var resolver = Should.Throw<ArgumentNullException>(() => new DefaultNetworkNameResolver(store, new FixedTimeProvider(), Options.Create(OptionsForNetwork()), null, null!));
        resolver.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public void Constructor_WhenLegacyLoggerArgumentIsNull_RetainsUnambiguousSourceCompatibility() => _ = new DefaultNetworkNameResolver(new TestGrantStore(), new FixedTimeProvider(), Options.Create(OptionsForNetwork()), null);
}
