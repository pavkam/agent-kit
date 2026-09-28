// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies ordered, filtered, and isolated durable execution event dispatch.</summary>
public sealed class DefaultDurableExecutionEventDispatcherTests
{
    private static readonly DurabilityProfileKey OtherProfile = new("other");

    [Fact]
    public async Task PublishAsync_WhenTheContextIsNull_ThrowsArgumentNullException()
    {
        using var provider = Compose(Declare<FirstSink>(order: 0));
        var dispatcher = provider.GetRequiredService<IDurableExecutionEventDispatcher>();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await dispatcher.PublishAsync(null!, Event(), TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public async Task PublishAsync_WhenTheEventIsNull_ThrowsArgumentNullException()
    {
        using var provider = Compose(Declare<FirstSink>(order: 0));
        var dispatcher = provider.GetRequiredService<IDurableExecutionEventDispatcher>();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await dispatcher.PublishAsync(
                DurableJournalTestData.Context(),
                null!,
                TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("executionEvent");
    }

    [Fact]
    public async Task PublishAsync_WhenAlreadyCancelled_ThrowsBeforeAnySinkObserves()
    {
        using var provider = Compose(Declare<FirstSink>(order: 0));
        var dispatcher = provider.GetRequiredService<IDurableExecutionEventDispatcher>();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        RecordingDurableExecutionEventSink.Log.Clear();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await dispatcher.PublishAsync(
                DurableJournalTestData.Context(),
                Event(),
                cancellation.Token));

        RecordingDurableExecutionEventSink.Log.ShouldBeEmpty();
    }

    [Fact]
    public async Task PublishAsync_WhenSinksAreRegisteredOutOfOrder_InvokesThemInAscendingDeclaredOrder()
    {
        using var provider = Compose(Declare<SecondSink>(order: 7), Declare<FirstSink>(order: 1));
        var dispatcher = provider.GetRequiredService<IDurableExecutionEventDispatcher>();
        RecordingDurableExecutionEventSink.Log.Clear();

        await dispatcher.PublishAsync(
            DurableJournalTestData.Context(),
            Event(),
            TestContext.Current.CancellationToken);

        RecordingDurableExecutionEventSink.Log.ShouldBe([nameof(FirstSink), nameof(SecondSink)]);
    }

    [Fact]
    public async Task PublishAsync_WhenASinkFiltersAnotherProfile_SkipsItWithoutResolvingTheSink()
    {
        using var provider = Compose(
            Declare<FirstSink>(order: 0, profiles: [OtherProfile]),
            Declare<SecondSink>(order: 1));
        var dispatcher = provider.GetRequiredService<IDurableExecutionEventDispatcher>();
        RecordingDurableExecutionEventSink.Log.Clear();

        await dispatcher.PublishAsync(
            DurableJournalTestData.Context(),
            Event(),
            TestContext.Current.CancellationToken);

        RecordingDurableExecutionEventSink.Log.ShouldBe([nameof(SecondSink)]);
    }

    [Fact]
    public async Task PublishAsync_WhenASinkDeclaresTheCapturedProfile_ObservesTheEvent()
    {
        using var provider = Compose(Declare<FirstSink>(order: 0, profiles: [new DurabilityProfileKey("profile")]));
        var dispatcher = provider.GetRequiredService<IDurableExecutionEventDispatcher>();
        var published = Event();

        await dispatcher.PublishAsync(
            DurableJournalTestData.Context(),
            published,
            TestContext.Current.CancellationToken);

        provider.GetRequiredService<FirstSink>().Accepted.ShouldBe([published]);
    }

    [Fact]
    public async Task PublishAsync_WhenAnObservationalSinkThrows_ContinuesWithTheRemainingSinks()
    {
        using var provider = Compose(Declare<FirstSink>(order: 0), Declare<SecondSink>(order: 1));
        provider.GetRequiredService<FirstSink>().Failure = new InvalidOperationException("sink down");
        var dispatcher = provider.GetRequiredService<IDurableExecutionEventDispatcher>();
        RecordingDurableExecutionEventSink.Log.Clear();

        await dispatcher.PublishAsync(
            DurableJournalTestData.Context(),
            Event(),
            TestContext.Current.CancellationToken);

        RecordingDurableExecutionEventSink.Log.ShouldBe([nameof(FirstSink), nameof(SecondSink)]);
        provider.GetRequiredService<SecondSink>().Accepted.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PublishAsync_WhenARequiredSinkThrows_FailsTheDispatchClosed()
    {
        // A caller must not be able to treat unobserved durable work as observed.
        using var provider = Compose(
            Declare<FirstSink>(order: 0, delivery: DurableExecutionEventDelivery.Required),
            Declare<SecondSink>(order: 1));
        provider.GetRequiredService<FirstSink>().Failure = new InvalidOperationException("sink down");
        var dispatcher = provider.GetRequiredService<IDurableExecutionEventDispatcher>();
        RecordingDurableExecutionEventSink.Log.Clear();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await dispatcher.PublishAsync(
                DurableJournalTestData.Context(),
                Event(),
                TestContext.Current.CancellationToken));

        _ = exception.InnerException.ShouldBeOfType<InvalidOperationException>();
        RecordingDurableExecutionEventSink.Log.ShouldBe([nameof(FirstSink)]);
    }

    [Fact]
    public async Task PublishAsync_WhenARequiredSinkIsUnavailable_FailsTheDispatchClosed()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentDurability();
        _ = services.AddSingleton(
            new DurableExecutionEventSinkDeclaration(
                new DurableExecutionEventSinkRegistration(
                    new DurableExecutionEventSinkId("missing"),
                    order: 0,
                    DurableExecutionEventDelivery.Required,
                    ServiceLifetime.Singleton),
                typeof(FirstSink)));
        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDurableExecutionEventDispatcher>();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await dispatcher.PublishAsync(
                DurableJournalTestData.Context(),
                Event(),
                TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("missing");
    }

    [Fact]
    public async Task PublishAsync_WhenAnObservationalSinkIsUnavailable_SkipsItAndContinues()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentDurability();
        _ = services.AddSingleton(
            new DurableExecutionEventSinkDeclaration(
                new DurableExecutionEventSinkRegistration(
                    new DurableExecutionEventSinkId("missing"),
                    order: 0,
                    DurableExecutionEventDelivery.Observational,
                    ServiceLifetime.Singleton),
                typeof(FirstSink)));
        _ = services.AddDurableExecutionEventSink<SecondSink>(Declare<SecondSink>(order: 1));
        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDurableExecutionEventDispatcher>();
        RecordingDurableExecutionEventSink.Log.Clear();


        await dispatcher.PublishAsync(
            DurableJournalTestData.Context(),
            Event(),
            TestContext.Current.CancellationToken);

        RecordingDurableExecutionEventSink.Log.ShouldBe([nameof(SecondSink)]);
    }

    [Fact]
    public async Task PublishAsync_WhenCancelledDuringASink_PropagatesAndStopsLaterSinks()
    {
        using var provider = Compose(Declare<CancellingSink>(order: 0), Declare<SecondSink>(order: 1));
        var dispatcher = provider.GetRequiredService<IDurableExecutionEventDispatcher>();
        using var cancellation = new CancellationTokenSource();
        provider.GetRequiredService<CancellingSink>().CancelWhileRunning = cancellation;
        RecordingDurableExecutionEventSink.Log.Clear();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await dispatcher.PublishAsync(
                DurableJournalTestData.Context(),
                Event(),
                cancellation.Token));

        RecordingDurableExecutionEventSink.Log.ShouldBe([nameof(CancellingSink)]);
    }

    [Fact]
    public async Task PublishAsync_WhenTheLoggerFails_StillDeliversToEverySink()
    {
        // Instrumentation is observational: a failing logging provider cannot change whether a sink observed an event.
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<ILogger<DefaultDurableExecutionEventDispatcher>>(
            new RecordingLogger<DefaultDurableExecutionEventDispatcher> { ThrowOnWrite = true });
        _ = services.AddAgentDurability();
        _ = services.AddDurableExecutionEventSink<FirstSink>(Declare<FirstSink>(order: 0));
        _ = services.AddDurableExecutionEventSink<SecondSink>(Declare<SecondSink>(order: 1));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<FirstSink>().Failure = new InvalidOperationException("sink down");
        var dispatcher = provider.GetRequiredService<IDurableExecutionEventDispatcher>();

        await dispatcher.PublishAsync(
            DurableJournalTestData.Context(),
            Event(),
            TestContext.Current.CancellationToken);

        provider.GetRequiredService<SecondSink>().Accepted.Count.ShouldBe(1);
    }

    private static DurableExecutionEventSinkRegistration Declare<TSink>(
        int order,
        DurableExecutionEventDelivery delivery = DurableExecutionEventDelivery.Observational,
        ImmutableArray<DurabilityProfileKey> profiles = default)
        where TSink : RecordingDurableExecutionEventSink =>
        new(new DurableExecutionEventSinkId(typeof(TSink).Name), order, delivery, ServiceLifetime.Singleton, profiles);

    private static DurableOperationAccepted Event() =>
        new DurableOperationAccepted(
            new DurableOperationBinding(DurableJournalTestData.Address(), DurableJournalTestData.Context()),
            DurableJournalTestData.Now,
            new DurableOperationName("tool.call"),
            new DurableOperationVersion("v1"),
            new FencingToken(1));

    private static ServiceProvider Compose(params DurableExecutionEventSinkRegistration[] registrations)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAgentDurability();
        foreach (var registration in registrations)
        {
            _ = registration.Id.Value switch
            {
                nameof(FirstSink) => services.AddDurableExecutionEventSink<FirstSink>(registration),
                nameof(SecondSink) => services.AddDurableExecutionEventSink<SecondSink>(registration),
                nameof(CancellingSink) => services.AddDurableExecutionEventSink<CancellingSink>(registration),
                _ => throw new ArgumentException("Unknown sink declaration.", nameof(registrations)),
            };
        }

        return services.BuildServiceProvider();
    }

    /// <summary>The first distinguishable sink type.</summary>
    private sealed class FirstSink: RecordingDurableExecutionEventSink;

    /// <summary>The second distinguishable sink type.</summary>
    private sealed class SecondSink: RecordingDurableExecutionEventSink;

    /// <summary>A sink used to cancel the publication while it is running.</summary>
    private sealed class CancellingSink: RecordingDurableExecutionEventSink;
}
