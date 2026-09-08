// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory.Tests;

public sealed class ScriptedNetworkBoundaryTests
{
    [Fact]
    public void Constructors_WhenLegacyLoggerArgumentIsNull_RetainUnambiguousSourceCompatibility()
    {
        var store = new TestGrantStore();

        _ = new ScriptedNetworkNameResolver(store, new FixedTimeProvider(), null);
        _ = new ScriptedNetworkTransport(store, new FixedTimeProvider(), null);
    }

    [Fact]
    public void Constructors_WhenIntentIdsNull_ThrowWithExactParameterName()
    {
        var store = new TestGrantStore();

        var resolver = Should.Throw<ArgumentNullException>(() => new ScriptedNetworkNameResolver(
            store, new FixedTimeProvider(), null, null!));
        var transport = Should.Throw<ArgumentNullException>(() => new ScriptedNetworkTransport(
            store, new FixedTimeProvider(), null, null!));

        resolver.ParamName.ShouldBe("intentIds");
        transport.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public async Task SendAsync_WhenObserved_EmitsContentFreeOperationActivity()
    {
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var transport = new ScriptedNetworkTransport(new TestGrantStore { Status = GrantConsumptionStatus.Mismatch }, new FixedTimeProvider());
        var request = Request();

        _ = await transport.SendAsync(request, TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.NetworkSend);
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.NetworkOperationId).ShouldBe(request.Id.ToString());
        string.Join('|', activity.TagObjects.Select(static tag => $"{tag.Key}={tag.Value}"))
            .ShouldNotContain("example.test");
    }

    [Fact]
    public async Task ResolveAsync_WhenGrantRejected_DoesNotConsultScenarioOrRecordTrace()
    {
        var store = new TestGrantStore { Status = GrantConsumptionStatus.Mismatch };
        var resolver = new ScriptedNetworkNameResolver(store, new FixedTimeProvider());
        resolver.Script(Destination(), new NetworkResolved([Address()]));
        var request = ResolutionRequest();

        var result = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<NetworkResolutionDenied>();
        resolver.Traces.ShouldBeEmpty();
        var enforcement = store.Enforcements.ShouldHaveSingleItem();
        enforcement.Audience.ShouldBe(resolver.SecurityAudience);
        enforcement.Resources.ShouldBe([NetworkSecurityBinding.ResolutionResource(request.Destination)]);
        enforcement.InputFingerprint.ShouldBe(NetworkSecurityBinding.ResolutionFingerprint(request));
    }

    [Fact]
    public async Task ResolveAsync_WhenStoreReconcilesAnEarlierIntent_DoesNotConsultScenarioOrRecordTrace()
    {
        var store = new TestGrantStore { Status = GrantConsumptionStatus.Reconciled };
        var resolver = new ScriptedNetworkNameResolver(store, new FixedTimeProvider());
        resolver.Script(Destination(), new NetworkResolved([Address()]));

        var result = await resolver.ResolveAsync(ResolutionRequest(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<NetworkResolutionDenied>();
        resolver.Traces.ShouldBeEmpty();
        _ = store.Intents.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ResolveAsync_WhenConsumedResultLacksExactReceipt_DoesNotConsultScenarioOrRecordTrace()
    {
        var store = new TestGrantStore { IncludeIntentReceipt = false };
        var resolver = new ScriptedNetworkNameResolver(store, new FixedTimeProvider());
        resolver.Script(Destination(), new NetworkResolved([Address()]));

        var result = await resolver.ResolveAsync(ResolutionRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<NetworkResolutionDenied>().SafeMessage.ShouldContain("enforcement-intent receipt");
        resolver.Traces.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WhenCallerAlreadyCancelled_DoesNotConsumeOrRecordTrace()
    {
        var store = new TestGrantStore();
        var resolver = new ScriptedNetworkNameResolver(store, new FixedTimeProvider());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = async () => await resolver.ResolveAsync(ResolutionRequest(), cancellation.Token);

        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        store.Enforcements.ShouldBeEmpty();
        resolver.Traces.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WhenAuthorized_ReturnsOrderedScenariosAndRetainsLastImmutableOutcome()
    {
        var resolver = new ScriptedNetworkNameResolver(new TestGrantStore(), new FixedTimeProvider());
        var first = new NetworkResolutionFailed(NetworkFailureKind.Timeout, "first");
        var second = new NetworkResolved([Address()]);
        resolver.Script(Destination(), first, second);

        var firstResult = await resolver.ResolveAsync(ResolutionRequest(), TestContext.Current.CancellationToken);
        var secondResult = await resolver.ResolveAsync(ResolutionRequest(), TestContext.Current.CancellationToken);
        var thirdResult = await resolver.ResolveAsync(ResolutionRequest(), TestContext.Current.CancellationToken);

        firstResult.ShouldBeSameAs(first);
        secondResult.ShouldBeSameAs(second);
        thirdResult.ShouldBeSameAs(second);
        resolver.Traces.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ResolveAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationEvidence()
    {
        var clock = new FixedTimeProvider();
        var store = new InMemorySecurityGrantStore(clock);
        var resolver = new ScriptedNetworkNameResolver(store, clock);
        resolver.Script(Destination(), new NetworkResolved([Address()]));
        var request = ResolutionRequest();
        var grant = TestSecurity.CapturedGrant(
            resolver.SecurityAudience,
            [NetworkSecurityBinding.ResolutionResource(request.Destination)],
            NetworkSecurityBinding.ResolutionFingerprint(request));
        request = new NetworkResolutionRequest(request.Id, request.Destination, request.Bounds, grant);
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);

        _ = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        _ = resolver.Traces.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SendAsync_WhenGrantRejected_DoesNotConsumeScenarioOrRecordTrace()
    {
        var store = new TestGrantStore { Status = GrantConsumptionStatus.Expired };
        var transport = new ScriptedNetworkTransport(store, new FixedTimeProvider());
        transport.Script(Destination(), new NetworkDenied("scripted"));
        var request = Request();

        var result = await transport.SendAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<NetworkDenied>().SafeMessage.ShouldBe("Denied.");
        transport.Traces.ShouldBeEmpty();
        var enforcement = store.Enforcements.ShouldHaveSingleItem();
        enforcement.Audience.ShouldBe(transport.SecurityAudience);
        enforcement.Resources.ShouldBe(NetworkSecurityBinding.RequestResources(request));
        enforcement.InputFingerprint.ShouldBe(NetworkSecurityBinding.RequestFingerprint(request));
    }

    [Fact]
    public async Task SendAsync_WhenStoreReconcilesAnEarlierIntent_DoesNotConsumeScenarioOrRecordTrace()
    {
        var store = new TestGrantStore { Status = GrantConsumptionStatus.Reconciled };
        var transport = new ScriptedNetworkTransport(store, new FixedTimeProvider());
        transport.Script(Destination(), new NetworkDenied("scripted"));

        var result = await transport.SendAsync(Request(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<NetworkDenied>();
        transport.Traces.ShouldBeEmpty();
        _ = store.Intents.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SendAsync_WhenCallerAlreadyCancelled_DoesNotConsumeOrRecordTrace()
    {
        var store = new TestGrantStore();
        var transport = new ScriptedNetworkTransport(store, new FixedTimeProvider());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = async () => await transport.SendAsync(Request(), cancellation.Token);

        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        store.Enforcements.ShouldBeEmpty();
        transport.Traces.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenCallerCancelsDuringNonCooperativeConsumption_DoesNotRecordTrace()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new TestGrantStore { OnIntentConsumption = cancellation.Cancel };
        var transport = new ScriptedNetworkTransport(store, new FixedTimeProvider());
        transport.Script(Destination(), new NetworkDenied("scripted"));

        var action = async () => await transport.SendAsync(Request(), cancellation.Token);

        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        _ = store.Intents.ShouldHaveSingleItem();
        transport.Traces.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenReceiptAccepted_UsesInjectedFreshIntentId()
    {
        var store = new TestGrantStore();
        var expectedId = new SecurityEnforcementIntentId(Guid.Parse("80000000-0000-0000-0000-000000000008"));
        var transport = new ScriptedNetworkTransport(
            store,
            new FixedTimeProvider(),
            null,
            new SequenceSecurityEnforcementIntentIdGenerator(expectedId.Value));
        transport.Script(Destination(), new NetworkDenied("scripted"));

        _ = await transport.SendAsync(Request(), TestContext.Current.CancellationToken);

        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(expectedId);
        store.LegacyConsumptionCalls.ShouldBe(0);
    }

    [Fact]
    public async Task AddAgentNetworkInMemory_WhenIntentGeneratorIsHostSupplied_UsesTheReplacement()
    {
        var store = new TestGrantStore();
        var expectedId = new SecurityEnforcementIntentId(Guid.Parse("81000000-0000-0000-0000-000000000008"));
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(
            new SequenceSecurityEnforcementIntentIdGenerator(expectedId.Value));
        _ = services.AddAgentNetworkInMemory();
        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<INetworkNameResolver>().ShouldBeOfType<ScriptedNetworkNameResolver>();
        resolver.Script(Destination(), new NetworkResolved([Address()]));

        _ = await resolver.ResolveAsync(ResolutionRequest(), TestContext.Current.CancellationToken);

        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(expectedId);
    }

    [Fact]
    public async Task SendAsync_WhenAuthorized_TransfersEachOwnedOutcomeOnce()
    {
        var transport = new ScriptedNetworkTransport(new TestGrantStore(), new FixedTimeProvider());
        var first = new NetworkDenied("first");
        var second = new NetworkDenied("second");
        transport.Script(Destination(), first, second);

        var firstResult = await transport.SendAsync(Request(), TestContext.Current.CancellationToken);
        var secondResult = await transport.SendAsync(Request(), TestContext.Current.CancellationToken);
        var exhausted = await transport.SendAsync(Request(), TestContext.Current.CancellationToken);

        firstResult.ShouldBeSameAs(first);
        secondResult.ShouldBeSameAs(second);
        _ = exhausted.ShouldBeOfType<NetworkRequestFailed>();
        transport.Traces.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SendAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationEvidence()
    {
        var clock = new FixedTimeProvider();
        var store = new InMemorySecurityGrantStore(clock);
        var transport = new ScriptedNetworkTransport(store, clock);
        transport.Script(Destination(), new NetworkDenied("scripted"));
        var request = Request();
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

        _ = await transport.SendAsync(request, TestContext.Current.CancellationToken);

        _ = transport.Traces.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ResolveAsync_WhenNoScenarioExists_ReturnsTypedFailure()
    {
        var resolver = new ScriptedNetworkNameResolver(new TestGrantStore(), new FixedTimeProvider());

        var result = await resolver.ResolveAsync(ResolutionRequest(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<NetworkResolutionFailed>().Kind.ShouldBe(NetworkFailureKind.DnsResolutionFailed);
    }

    [Fact]
    public void AddAgentNetworkInMemory_WhenCalledTwice_UsesOneSharedScriptedBoundary()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore, TestGrantStore>();

        _ = services.AddAgentNetworkInMemory().AddAgentNetworkInMemory();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<INetworkNameResolver>()
            .ShouldBeSameAs(provider.GetRequiredService<ScriptedNetworkNameResolver>());
        provider.GetRequiredService<INetworkTransport>()
            .ShouldBeSameAs(provider.GetRequiredService<ScriptedNetworkTransport>());
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

    [Fact]
    public void Script_WhenScenarioEmpty_ThrowsBeforeReplacingExistingScenario()
    {
        var resolver = new ScriptedNetworkNameResolver(new TestGrantStore(), new FixedTimeProvider());
        resolver.Script(Destination(), new NetworkResolved([Address()]));

        var action = () => resolver.Script(Destination(), []);

        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("results");
    }

    private static NetworkDestination Destination() => new(
        "https",
        new NormalizedHost("example.test"),
        443,
        NetworkRoute.Root);

    private static NetworkBounds Bounds() => new(
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        1_024,
        3);

    private static NetworkAddress Address() => new(
        IPAddress.Parse("192.0.2.1"),
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch.AddMinutes(1));

    private static NetworkResolutionRequest ResolutionRequest() => new(
        new NetworkOperationId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
        Destination(),
        Bounds(),
        TestSecurity.Grant());

    private static NetworkRequest Request() => new(
        new NetworkOperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
        NetworkMethod.Get,
        Destination(),
        NetworkHeaderSet.Empty,
        null,
        Bounds(),
        [Address()],
        NetworkDataClassification.Public,
        TestSecurity.Grant());

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;
}
