// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

/// <summary>Verifies DefaultCompactionEventDispatcher behavior and contracts.</summary>
public sealed class DefaultCompactionEventDispatcherTests
{
    private static readonly ComponentKey<ICompactor> _key = new("dispatcher-compactor");

    [Fact]
    public async Task PublishAsync_WhenEventIsNull_ThrowsArgumentNullException()
    {
        using var provider = Provider(static _ => { });

        var failure = await Should.ThrowAsync<ArgumentNullException>(
            async () => await Dispatcher(provider).PublishAsync(_key, null!, TestContext.Current.CancellationToken));

        failure.ParamName.ShouldBe("compactionEvent");
    }

    [Fact]
    public async Task PublishAsync_WhenCompactorKeyIsDefault_ThrowsArgumentOutOfRangeException()
    {
        using var provider = Provider(static _ => { });

        var failure = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            async () => await Dispatcher(provider).PublishAsync(default, Event(), TestContext.Current.CancellationToken));

        failure.ParamName.ShouldBe("compactorKey");
    }

    [Fact]
    public async Task PublishAsync_WhenCompactorKeyIsAnotherCompactor_DeliversNothing()
    {
        var log = new CompactionEventLog();
        using var provider = Provider(
            services => services.AddSingleton(log).AddCompactionEventSink<RecordingCompactionEventSink>(_key, Sink("s")));

        var result = await Dispatcher(provider).PublishAsync(
            new ComponentKey<ICompactor>("another"), Event(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<CompactionEventPublished>();
        log.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task PublishAsync_WhenABestEffortSinkThrows_ContinuesToLaterSinks()
    {
        var log = new CompactionEventLog();
        using var provider = Provider(services =>
        {
            _ = services.AddSingleton(log);
            _ = services.AddCompactionEventSink<ThrowingCompactionEventSink>(_key, Sink("a", order: 0));
            _ = services.AddCompactionEventSink<RecordingCompactionEventSink>(_key, Sink("b", order: 1));
        });

        var result = await Dispatcher(provider).PublishAsync(_key, Event(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<CompactionEventPublished>();
        log.Entries.ShouldBe([nameof(RecordingCompactionEventSink)]);
    }

    [Fact]
    public async Task PublishAsync_WhenARequiredSinkThrows_StopsAndReportsThatSink()
    {
        var log = new CompactionEventLog();
        using var provider = Provider(services =>
        {
            _ = services.AddSingleton(log);
            _ = services.AddCompactionEventSink<ThrowingCompactionEventSink>(_key, Sink("a", order: 0, CompactionEventDelivery.Required));
            _ = services.AddCompactionEventSink<RecordingCompactionEventSink>(_key, Sink("b", order: 1));
        });

        var result = await Dispatcher(provider).PublishAsync(_key, Event(), TestContext.Current.CancellationToken);

        var unavailable = result.ShouldBeOfType<RequiredCompactionEventUnavailable>();
        unavailable.SinkId.ShouldBe(new CompactionEventSinkId("a"));
        unavailable.Failure.Kind.ShouldBe(CompactionFailureKind.RequiredObservationUnavailable);
        unavailable.Failure.SafeMessage.ShouldNotContain("sink failed");
        log.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task PublishAsync_WhenCancellationIsRequested_PropagatesInsteadOfSwallowingIt()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var provider = Provider(services =>
            services.AddCompactionEventSink<CancellingCompactionEventSink>(_key, Sink("a")));

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await Dispatcher(provider).PublishAsync(_key, Event(), cancellation.Token));
    }

    [Fact]
    public async Task PublishAsync_WhenASinkRegistrationHasNoServiceBehindIt_ReportsRequiredSinksUnavailableAndSkipsBestEffortOnes()
    {
        var services = new ServiceCollection();
        var dispatcher = new DefaultCompactionEventDispatcher(
            _key,
            [
                new CompactionEventSinkDeclaration(_key.Value, Sink("optional", order: 0), typeof(RecordingCompactionEventSink)),
                new CompactionEventSinkDeclaration(_key.Value, Sink("audit", order: 1, CompactionEventDelivery.Required), typeof(RecordingCompactionEventSink)),
            ],
            services.BuildServiceProvider());

        var result = await dispatcher.PublishAsync(_key, Event(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RequiredCompactionEventUnavailable>().SinkId.ShouldBe(new CompactionEventSinkId("audit"));
    }

    private static ICompactionEventDispatcher Dispatcher(IServiceProvider provider) =>
        provider.GetRequiredKeyedService<ICompactionEventDispatcher>(CompactionServiceKeys.EventDispatcher(_key));

    private static CompactionEventSinkRegistration Sink(
        string id, int order = 0, CompactionEventDelivery delivery = CompactionEventDelivery.BestEffort) =>
        new(new CompactionEventSinkId(id), order, delivery, ServiceLifetime.Singleton);

    private static CompactionAttemptStartedEvent Event() =>
        new(
            TestFactory.CompactionContext(),
            DateTimeOffset.UnixEpoch,
            new CompactionTrigger(CompactionTriggerKind.ExplicitMaintenance, "test", null));

    private static ServiceProvider Provider(Action<IServiceCollection> customize)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISessionCoordinator>(new FakeSessionCoordinator(new BranchId(Guid.NewGuid())));
        _ = services.AddAgentContextCompaction(_key);
        customize(services);
        return services.BuildServiceProvider();
    }
}
