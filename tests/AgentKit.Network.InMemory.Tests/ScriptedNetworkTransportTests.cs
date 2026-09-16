// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory.Tests;



/// <summary>Verifies ScriptedNetworkTransport behavior and contracts.</summary>
public sealed class ScriptedNetworkTransportTests
{
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
        string.Join('|', activity.TagObjects.Select(static tag => $"{tag.Key}={tag.Value}")).ShouldNotContain("example.test");
    }

    [Fact]
    public async Task SendAsync_WhenGrantRejected_DoesNotConsumeScenarioOrRecordTrace()
    {
        var store = new TestGrantStore
        {
            Status = GrantConsumptionStatus.Expired
        };
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
        var store = new TestGrantStore
        {
            Status = GrantConsumptionStatus.Reconciled
        };
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
        var store = new TestGrantStore
        {
            OnIntentConsumption = cancellation.Cancel
        };
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
        var transport = new ScriptedNetworkTransport(store, new FixedTimeProvider(), null, new SequenceSecurityEnforcementIntentIdGenerator(expectedId.Value));
        transport.Script(Destination(), new NetworkDenied("scripted"));
        _ = await transport.SendAsync(Request(), TestContext.Current.CancellationToken);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(expectedId);
        store.LegacyConsumptionCalls.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_WhenScenarioSucceeds_RecordsSuccessfulOutcome()
    {
        var transport = new ScriptedNetworkTransport(new TestGrantStore(), new FixedTimeProvider());
        var metadata = new NetworkResponseMetadata(200, NetworkHeaderSet.Empty, 0);
        var response = new NetworkResponseReceived(new ScriptedNetworkResponse(metadata, ReadOnlyMemory<byte>.Empty));
        transport.Script(Destination(), response);
        var result = await transport.SendAsync(Request(), TestContext.Current.CancellationToken);
        result.ShouldBeSameAs(response);
        await response.Response.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_WhenActionThrowsUnexpectedException_PropagatesAfterObservingFailure()
    {
        var store = new TestGrantStore { OnIntentConsumption = static () => throw new InvalidOperationException("boom") };
        var transport = new ScriptedNetworkTransport(store, new FixedTimeProvider());
        var action = async () => await transport.SendAsync(Request(), TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldBe("boom");
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
        var grant = TestSecurity.CapturedGrant(transport.SecurityAudience, NetworkSecurityBinding.RequestResources(request), NetworkSecurityBinding.RequestFingerprint(request));
        request = new NetworkRequest(request.Id, request.Method, request.Destination, request.Headers, request.Content, request.Bounds, request.ResolvedAddresses, request.Classification, grant);
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        _ = await transport.SendAsync(request, TestContext.Current.CancellationToken);
        _ = transport.Traces.ShouldHaveSingleItem();
    }

    private static NetworkDestination Destination() => new("https", new NormalizedHost("example.test"), 443, NetworkRoute.Root);
    private static NetworkBounds Bounds() => new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 1_024, 3);
    private static NetworkAddress Address() => new(IPAddress.Parse("192.0.2.1"), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1));
    private static NetworkRequest Request() => new(new NetworkOperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")), NetworkMethod.Get, Destination(), NetworkHeaderSet.Empty, null, Bounds(), [Address()], NetworkDataClassification.Public, TestSecurity.Grant());
    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
    [Fact]
    public void Constructors_WhenIntentIdsNull_ThrowWithExactParameterName()
    {
        var store = new TestGrantStore();
        var transport = Should.Throw<ArgumentNullException>(() => new ScriptedNetworkTransport(store, new FixedTimeProvider(), null, null!));
        transport.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public void Constructor_WhenLegacyLoggerArgumentIsNull_RetainsUnambiguousSourceCompatibility() => _ = new ScriptedNetworkTransport(new TestGrantStore(), new FixedTimeProvider(), null);
}
