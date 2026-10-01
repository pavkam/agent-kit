// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

using AgentKit.Observability;

/// <summary>Verifies <see cref="ArtifactEventDispatcher"/> ordering, lifetime handling, isolation, and metrics.</summary>
public sealed class ArtifactEventDispatcherTests
{
    private static readonly ArtifactPreparedEvent _event = new(
        ArtifactTestData.CoordinatorKey, ArtifactTestData.Identity.TenantId, ArtifactTestData.ProfileKey, ArtifactTestData.Now,
        new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        new ArtifactPreparationId(Guid.Parse("20000000-0000-0000-0000-000000000002")), new ArtifactVersion("1"));

    [Fact]
    public void Constructor_WhenADependencyIsNull_ThrowsNamingIt()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        Should.Throw<ArgumentNullException>(() => new ArtifactEventDispatcher(null!, provider)).ParamName.ShouldBe("declarations");
        Should.Throw<ArgumentNullException>(() => new ArtifactEventDispatcher([], null!)).ParamName.ShouldBe("services");
    }

    [Fact]
    public async Task PublishAsync_WhenArgumentsAreInvalid_ThrowsNamingThem()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var dispatcher = new ArtifactEventDispatcher([], provider);

        (await Should.ThrowAsync<ArgumentException>(async () => await dispatcher.PublishAsync(default, _event))).ParamName.ShouldBe("coordinatorKey");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await dispatcher.PublishAsync(ArtifactTestData.CoordinatorKey, null!))).ParamName.ShouldBe("artifactEvent");
    }

    [Fact]
    public async Task PublishAsync_WhenSinksAreRegistered_DeliversInOrderThenIdentityToOnlyTheirCoordinator()
    {
        var delivered = new List<string>();
        var services = new ServiceCollection();
        _ = services.AddSingleton(new OrderedSinks(delivered));
        _ = services.AddArtifactEventSink<SinkB>(ArtifactTestData.CoordinatorKey, new ArtifactEventSinkRegistration(new ArtifactEventSinkId("b"), 1, ServiceLifetime.Singleton));
        _ = services.AddArtifactEventSink<SinkA>(ArtifactTestData.CoordinatorKey, new ArtifactEventSinkRegistration(new ArtifactEventSinkId("a"), 1, ServiceLifetime.Transient));
        _ = services.AddArtifactEventSink<SinkFirst>(ArtifactTestData.CoordinatorKey, new ArtifactEventSinkRegistration(new ArtifactEventSinkId("first"), 0, ServiceLifetime.Scoped));
        _ = services.AddArtifactEventSink<SinkOther>(new ComponentKey<IArtifactCoordinator>("other"), new ArtifactEventSinkRegistration(new ArtifactEventSinkId("other"), -5, ServiceLifetime.Singleton));
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IArtifactEventDispatcher>().PublishAsync(ArtifactTestData.CoordinatorKey, _event, TestContext.Current.CancellationToken);

        delivered.ShouldBe(["first", "a", "b"]);
    }

    [Fact]
    public async Task PublishAsync_WhenASinkFailsOrIsMissing_IsolatesItCountsItAndContinues()
    {
        var logger = new RecordingLogger<ArtifactEventDispatcher>();
        var delivered = new List<string>();
        var services = new ServiceCollection();
        _ = services.AddSingleton(new OrderedSinks(delivered));
        _ = services.AddSingleton<ILogger<ArtifactEventDispatcher>>(logger);
        _ = services.AddArtifactEventSink<FailingSink>(ArtifactTestData.CoordinatorKey, new ArtifactEventSinkRegistration(new ArtifactEventSinkId("failing"), 0, ServiceLifetime.Singleton));
        _ = services.AddArtifactEventSink<SinkA>(ArtifactTestData.CoordinatorKey, new ArtifactEventSinkRegistration(new ArtifactEventSinkId("a"), 1, ServiceLifetime.Singleton));
        _ = services.AddSingleton(new ArtifactEventSinkDeclaration(
            ArtifactTestData.CoordinatorKey, new ArtifactEventSinkRegistration(new ArtifactEventSinkId("missing"), 2, ServiceLifetime.Singleton), typeof(MissingSink)));
        using var provider = services.BuildServiceProvider();
        using var metrics = new MetricCollector(AgentKitMetricNames.ArtifactEventSinkFailureCount);

        await provider.GetRequiredService<IArtifactEventDispatcher>().PublishAsync(ArtifactTestData.CoordinatorKey, _event, TestContext.Current.CancellationToken);

        delivered.ShouldBe(["a"]);
        var entries = logger.Snapshot();
        entries.Select(static entry => entry.EventId.Id).Order().ShouldBe([29003, 29004]);
        entries.ShouldAllBe(static entry => entry.Level == LogLevel.Warning);
        var observed = metrics.Snapshot().Select(static measurement => measurement.Tags[AgentKitTagNames.Outcome]!.ToString()).ToArray();
        observed.ShouldContain("failed");
        observed.ShouldContain("unavailable");
    }

    [Fact]
    public async Task PublishAsync_WhenCancelled_PropagatesCancellationInsteadOfIsolatingIt()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(new OrderedSinks([]));
        _ = services.AddArtifactEventSink<CancellingSink>(ArtifactTestData.CoordinatorKey, new ArtifactEventSinkRegistration(new ArtifactEventSinkId("cancelling"), 0, ServiceLifetime.Singleton));
        using var provider = services.BuildServiceProvider();
        using var cancelled = new CancellationTokenSource();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await cancelled.CancelAsync();
            await provider.GetRequiredService<IArtifactEventDispatcher>().PublishAsync(ArtifactTestData.CoordinatorKey, _event, cancelled.Token);
        });
    }

    [Fact]
    public async Task PublishAsync_WhenNoSinkObservesTheCoordinator_CompletesWithoutResolvingAnything()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        await new ArtifactEventDispatcher([], provider).PublishAsync(ArtifactTestData.CoordinatorKey, _event, TestContext.Current.CancellationToken);
    }

    private sealed record OrderedSinks(List<string> Delivered);

    private abstract class NamedSink(OrderedSinks sinks, string name): IArtifactEventSink
    {
        public ValueTask PublishAsync(ArtifactEvent artifactEvent, CancellationToken cancellationToken = default)
        {
            sinks.Delivered.Add(name);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SinkFirst(OrderedSinks sinks): NamedSink(sinks, "first");

    private sealed class SinkA(OrderedSinks sinks): NamedSink(sinks, "a");

    private sealed class SinkB(OrderedSinks sinks): NamedSink(sinks, "b");

    private sealed class SinkOther(OrderedSinks sinks): NamedSink(sinks, "other");

    private sealed class FailingSink: IArtifactEventSink
    {
        public ValueTask PublishAsync(ArtifactEvent artifactEvent, CancellationToken cancellationToken = default) => throw new InvalidOperationException("sink failed");
    }

    private sealed class CancellingSink: IArtifactEventSink
    {
        public ValueTask PublishAsync(ArtifactEvent artifactEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MissingSink: IArtifactEventSink
    {
        public ValueTask PublishAsync(ArtifactEvent artifactEvent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
