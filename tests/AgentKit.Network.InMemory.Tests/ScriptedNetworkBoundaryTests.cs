// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory.Tests;

public sealed class ScriptedNetworkBoundaryTests
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
