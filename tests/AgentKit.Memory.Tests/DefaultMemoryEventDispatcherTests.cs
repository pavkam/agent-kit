// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

/// <summary>Verifies sink ordering, profile filtering, and required versus observational failure accounting.</summary>
public sealed class DefaultMemoryEventDispatcherTests
{
    private sealed class OrderLog
    {
        internal List<string> Entries { get; } = [];
    }

    private sealed class FirstSink(OrderLog log): IMemoryEventSink
    {
        public ValueTask PublishAsync(MemoryEvent memoryEvent, CancellationToken cancellationToken = default)
        {
            log.Entries.Add("first");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SecondSink(OrderLog log): IMemoryEventSink
    {
        public ValueTask PublishAsync(MemoryEvent memoryEvent, CancellationToken cancellationToken = default)
        {
            log.Entries.Add("second");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingSink: IMemoryEventSink
    {
        public ValueTask PublishAsync(MemoryEvent memoryEvent, CancellationToken cancellationToken = default) => throw new InvalidOperationException("The sink failed.");
    }

    private sealed class CancellingSink: IMemoryEventSink
    {
        public ValueTask PublishAsync(MemoryEvent memoryEvent, CancellationToken cancellationToken = default) => throw new OperationCanceledException(cancellationToken);
    }

    private static MemoryEvent Event() => new(
        MemoryEventKind.ProposalAccepted, new TenantId("tenant"), new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), MemoryTestData.ProfileKey,
        MemoryTestData.ProfileVersion, new MemoryId(Guid.NewGuid()), null, null, "accepted", null, MemoryTestData.Now);

    private static MemoryEventSinkRegistration Sink(string id, int order, MemoryEventDelivery delivery, params MemoryProfileKey[] profiles) =>
        new(new ComponentId(id), order, delivery, ServiceLifetime.Singleton, [.. profiles]);

    [Fact]
    public async Task PublishAsync_WhenNoSinkIsRegistered_ReturnsNone()
    {
        using var harness = MemoryHarness.Create();

        var result = await harness.Provider.GetRequiredService<IMemoryEventDispatcher>().PublishAsync(MemoryTestData.ProfileKey, Event(), TestContext.Current.CancellationToken);

        result.ShouldBe(MemoryEventDispatchResult.None);
    }

    [Fact]
    public async Task PublishAsync_WhenSinksHaveOrders_DeliversInAscendingOrder()
    {
        using var harness = MemoryHarness.Create(arrange: services =>
        {
            _ = services.AddSingleton<OrderLog>();
            _ = services.AddMemoryEventSink<SecondSink>(Sink("b", 2, MemoryEventDelivery.Observational));
            _ = services.AddMemoryEventSink<FirstSink>(Sink("a", 1, MemoryEventDelivery.Observational));
        });

        var result = await harness.Provider.GetRequiredService<IMemoryEventDispatcher>().PublishAsync(MemoryTestData.ProfileKey, Event(), TestContext.Current.CancellationToken);

        result.Delivered.ShouldBe(2);
        harness.Provider.GetRequiredService<OrderLog>().Entries.ShouldBe(["first", "second"]);
    }

    [Fact]
    public async Task PublishAsync_WhenASinkObservesOtherProfiles_SkipsIt()
    {
        using var harness = MemoryHarness.Create(arrange: services =>
        {
            _ = services.AddSingleton<OrderLog>();
            _ = services.AddMemoryEventSink<FirstSink>(Sink("a", 1, MemoryEventDelivery.Observational, new MemoryProfileKey("other")));
        });

        var result = await harness.Provider.GetRequiredService<IMemoryEventDispatcher>().PublishAsync(MemoryTestData.ProfileKey, Event(), TestContext.Current.CancellationToken);

        result.Delivered.ShouldBe(0);
    }

    [Fact]
    public async Task PublishAsync_WhenAnObservationalSinkFails_CountsItWithoutFailingRequiredDelivery()
    {
        using var harness = MemoryHarness.Create(arrange: services =>
            services.AddMemoryEventSink<ThrowingSink>(Sink("a", 1, MemoryEventDelivery.Observational)));

        var result = await harness.Provider.GetRequiredService<IMemoryEventDispatcher>().PublishAsync(MemoryTestData.ProfileKey, Event(), TestContext.Current.CancellationToken);

        result.ObservationalFailures.ShouldBe(1);
        result.RequiredDeliveryComplete.ShouldBeTrue();
    }

    [Fact]
    public async Task PublishAsync_WhenARequiredSinkFails_ReportsIncompleteRequiredDelivery()
    {
        using var harness = MemoryHarness.Create(arrange: services =>
            services.AddMemoryEventSink<ThrowingSink>(Sink("a", 1, MemoryEventDelivery.Required)));

        var result = await harness.Provider.GetRequiredService<IMemoryEventDispatcher>().PublishAsync(MemoryTestData.ProfileKey, Event(), TestContext.Current.CancellationToken);

        result.RequiredFailures.ShouldBe(1);
        result.RequiredDeliveryComplete.ShouldBeFalse();
    }

    private sealed class UnresolvableSink: IMemoryEventSink
    {
        public ValueTask PublishAsync(MemoryEvent memoryEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Never resolved.");
    }

    [Fact]
    public async Task PublishAsync_WhenARequiredSinkCannotBeResolved_LogsTheUnavailableEventAndReportsTheFailure()
    {
        var logger = new RecordingLogger<DefaultMemoryEventDispatcher>();
        using var harness = MemoryHarness.Create(arrange: services =>
        {
            _ = services.AddSingleton<ILogger<DefaultMemoryEventDispatcher>>(logger);
            _ = services.AddSingleton(new MemoryEventSinkDeclaration(Sink("tests.unresolvable", 1, MemoryEventDelivery.Required), typeof(UnresolvableSink)));
        });

        var result = await harness.Provider.GetRequiredService<IMemoryEventDispatcher>().PublishAsync(MemoryTestData.ProfileKey, Event(), TestContext.Current.CancellationToken);

        result.RequiredFailures.ShouldBe(1);
        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(32302);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain("tests.unresolvable");
    }

    [Fact]
    public async Task PublishAsync_WhenASinkFails_LogsTheFailedEventWithTheErrorTypeAndNoMessage()
    {
        var logger = new RecordingLogger<DefaultMemoryEventDispatcher>();
        using var harness = MemoryHarness.Create(arrange: services =>
        {
            _ = services.AddSingleton<ILogger<DefaultMemoryEventDispatcher>>(logger);
            _ = services.AddMemoryEventSink<ThrowingSink>(Sink("a", 1, MemoryEventDelivery.Observational));
        });

        _ = await harness.Provider.GetRequiredService<IMemoryEventDispatcher>().PublishAsync(MemoryTestData.ProfileKey, Event(), TestContext.Current.CancellationToken);

        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(32303);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain(nameof(InvalidOperationException));
        entry.Message.ShouldNotContain("The sink failed.");
    }

    [Fact]
    public async Task PublishAsync_WhenASinkIsCancelled_PropagatesCancellation()
    {
        using var harness = MemoryHarness.Create(arrange: services =>
            services.AddMemoryEventSink<CancellingSink>(Sink("a", 1, MemoryEventDelivery.Required)));
        using var source = new CancellationTokenSource();
        var dispatcher = harness.Provider.GetRequiredService<IMemoryEventDispatcher>();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await source.CancelAsync();
            _ = await dispatcher.PublishAsync(MemoryTestData.ProfileKey, Event(), source.Token);
        });
    }

    [Fact]
    public async Task PublishAsync_WhenArgumentsAreInvalid_Throws()
    {
        using var harness = MemoryHarness.Create();
        var dispatcher = harness.Provider.GetRequiredService<IMemoryEventDispatcher>();

        (await Should.ThrowAsync<ArgumentException>(async () => await dispatcher.PublishAsync(default, Event(), TestContext.Current.CancellationToken))).ParamName.ShouldBe("profile");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await dispatcher.PublishAsync(MemoryTestData.ProfileKey, null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("memoryEvent");
    }
}
