// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory.Tests;



/// <summary>Verifies ScriptedNetworkNameResolver behavior and contracts.</summary>
public sealed class ScriptedNetworkNameResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenGrantRejected_DoesNotConsultScenarioOrRecordTrace()
    {
        var store = new TestGrantStore
        {
            Status = GrantConsumptionStatus.Mismatch
        };
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
        var store = new TestGrantStore
        {
            Status = GrantConsumptionStatus.Reconciled
        };
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
        var store = new TestGrantStore
        {
            IncludeIntentReceipt = false
        };
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
        var grant = TestSecurity.CapturedGrant(resolver.SecurityAudience, [NetworkSecurityBinding.ResolutionResource(request.Destination)], NetworkSecurityBinding.ResolutionFingerprint(request));
        request = new NetworkResolutionRequest(request.Id, request.Destination, request.Bounds, grant);
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        _ = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);
        _ = resolver.Traces.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ResolveAsync_WhenActionThrowsUnexpectedException_PropagatesAfterObservingFailure()
    {
        var store = new TestGrantStore { OnIntentConsumption = static () => throw new InvalidOperationException("boom") };
        var resolver = new ScriptedNetworkNameResolver(store, new FixedTimeProvider());
        var action = async () => await resolver.ResolveAsync(ResolutionRequest(), TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldBe("boom");
    }

    [Fact]
    public async Task ResolveAsync_WhenNoScenarioExists_ReturnsTypedFailure()
    {
        var resolver = new ScriptedNetworkNameResolver(new TestGrantStore(), new FixedTimeProvider());
        var result = await resolver.ResolveAsync(ResolutionRequest(), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<NetworkResolutionFailed>().Kind.ShouldBe(NetworkFailureKind.DnsResolutionFailed);
    }

    [Fact]
    public void Script_WhenScenarioEmpty_ThrowsBeforeReplacingExistingScenario()
    {
        var resolver = new ScriptedNetworkNameResolver(new TestGrantStore(), new FixedTimeProvider());
        resolver.Script(Destination(), new NetworkResolved([Address()]));
        var action = () => resolver.Script(Destination(), []);
        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("results");
    }

    private static NetworkDestination Destination() => new("https", new NormalizedHost("example.test"), 443, NetworkRoute.Root);
    private static NetworkBounds Bounds() => new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 1_024, 3);
    private static NetworkAddress Address() => new(IPAddress.Parse("192.0.2.1"), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1));
    private static NetworkResolutionRequest ResolutionRequest() => new(new NetworkOperationId(Guid.Parse("50000000-0000-0000-0000-000000000005")), Destination(), Bounds(), TestSecurity.Grant());
    [Fact]
    public void Constructors_WhenIntentIdsNull_ThrowWithExactParameterName()
    {
        var store = new TestGrantStore();
        var resolver = Should.Throw<ArgumentNullException>(() => new ScriptedNetworkNameResolver(store, new FixedTimeProvider(), null, null!));
        resolver.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public void Constructor_WhenLegacyLoggerArgumentIsNull_RetainsUnambiguousSourceCompatibility() => _ = new ScriptedNetworkNameResolver(new TestGrantStore(), new FixedTimeProvider(), null);
}
