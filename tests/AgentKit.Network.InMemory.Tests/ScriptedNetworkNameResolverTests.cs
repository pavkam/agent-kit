// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory.Tests;

public sealed class ScriptedNetworkNameResolverTests
{
    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ScriptedNetworkNameResolver(null!));

        exception.ParamName.ShouldBe("timeProvider");
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
    public async Task ResolveAsync_WhenDestinationIsUnscripted_ReturnsResolutionFailed()
    {
        var resolver = TestFactory.Resolver();

        var result = await resolver.ResolveAsync(TestFactory.ResolutionRequest(), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<NetworkResolutionFailed>();
        failed.Kind.ShouldBe(NetworkFailureKind.DnsResolutionFailed);
    }

    [Fact]
    public async Task ResolveAsync_WhenScripted_ReturnsScriptedResult()
    {
        var resolver = TestFactory.Resolver();
        var destination = TestFactory.Destination();
        var scripted = new NetworkResolved([TestFactory.Address()]);
        resolver.Script(destination, scripted);

        var result = await resolver.ResolveAsync(
            TestFactory.ResolutionRequest(destination), TestContext.Current.CancellationToken);

        result.ShouldBe(scripted);
    }

    [Fact]
    public async Task ResolveAsync_WhenMultipleResultsScripted_ReturnsThemInOrderThenSticksOnLast()
    {
        var resolver = TestFactory.Resolver();
        var destination = TestFactory.Destination();
        var first = new NetworkResolutionFailed(NetworkFailureKind.Timeout, "first attempt failed");
        var second = new NetworkResolved([TestFactory.Address()]);
        resolver.Script(destination, first, second);

        var firstResult = await resolver.ResolveAsync(TestFactory.ResolutionRequest(destination), TestContext.Current.CancellationToken);
        var secondResult = await resolver.ResolveAsync(TestFactory.ResolutionRequest(destination), TestContext.Current.CancellationToken);
        var thirdResult = await resolver.ResolveAsync(TestFactory.ResolutionRequest(destination), TestContext.Current.CancellationToken);

        firstResult.ShouldBe(first);
        secondResult.ShouldBe(second);
        thirdResult.ShouldBe(second);
    }

    [Fact]
    public void Script_WhenDestinationIsNull_ThrowsArgumentNullException()
    {
        var resolver = TestFactory.Resolver();

        var exception = Should.Throw<ArgumentNullException>(
            () => resolver.Script(null!, new NetworkResolved([TestFactory.Address()])));

        exception.ParamName.ShouldBe("destination");
    }

    [Fact]
    public void Script_WhenNoResultsSupplied_ThrowsArgumentException()
    {
        var resolver = TestFactory.Resolver();

        _ = Should.Throw<ArgumentException>(() => resolver.Script(TestFactory.Destination()));
    }

    [Fact]
    public async Task ResolveAsync_AlwaysRecordsTrace_EvenWhenUnscripted()
    {
        var resolver = TestFactory.Resolver();
        var request = TestFactory.ResolutionRequest();

        _ = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        resolver.Traces.Count.ShouldBe(1);
        resolver.Traces[0].Id.ShouldBe(request.Id);
        resolver.Traces[0].Phase.ShouldBe("resolve");
    }

    [Fact]
    public async Task ResolveAsync_UsesInjectedTimeProviderForTraceTimestamp()
    {
        var timeProvider = new FakeTimeProvider();
        var resolver = TestFactory.Resolver(timeProvider);

        _ = await resolver.ResolveAsync(TestFactory.ResolutionRequest(), TestContext.Current.CancellationToken);

        resolver.Traces[0].At.ShouldBe(timeProvider.GetUtcNow());
    }
}
