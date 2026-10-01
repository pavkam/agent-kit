// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

public sealed class GoalEventDispatcherTests
{
    private sealed class CollectingSink: IGoalEventSink
    {
        internal ConcurrentQueue<GoalEvent> Seen { get; } = [];

        public ValueTask PublishAsync(GoalEvent goalEvent, CancellationToken cancellationToken = default)
        {
            Seen.Enqueue(goalEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingSink: IGoalEventSink
    {
        public ValueTask PublishAsync(GoalEvent goalEvent, CancellationToken cancellationToken = default) => throw new InvalidOperationException("sink failed");
    }

    private static GoalEvent Event(GoalProfileKey? profile = null) => new(
        GoalEventKind.DelegationDispatched, new GoalId(Guid.NewGuid()), null, new TenantId("t"), GoalTestData.NewAgent(), GoalTestData.NewSession(),
        profile ?? GoalTestData.Profile.Key, null, null, null, GoalTestData.Now);

    private static IGoalEventDispatcher Build(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        _ = services.AddSingleton<ISecurityGrantStore>(new TestGoalGrants());
        _ = services.AddAgentGoals();
        register(services);
        return services.BuildServiceProvider().GetRequiredService<IGoalEventDispatcher>();
    }

    [Fact]
    public async Task PublishAsync_WhenEventIsNull_ThrowsArgumentNullException()
    {
        var dispatcher = Build(static _ => { });

        (await Should.ThrowAsync<ArgumentNullException>(async () => await dispatcher.PublishAsync(null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("goalEvent");
    }

    [Fact]
    public async Task PublishAsync_WhenNoSinksAreRegistered_Completes() =>
        await Build(static _ => { }).PublishAsync(Event(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task PublishAsync_WhenSinksAreOrdered_DeliversToEachForMatchingProfiles()
    {
        var sink = new CollectingSink();

        await Build(services =>
        {
            _ = services.AddGoalEventSink<CollectingSink>(new GoalEventSinkRegistration(new ComponentId("all"), 1, GoalEventDelivery.Observational, ServiceLifetime.Singleton));
            _ = services.AddSingleton(sink);
        }).PublishAsync(Event(), TestContext.Current.CancellationToken);

        sink.Seen.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PublishAsync_WhenSinkObservesOtherProfile_SkipsIt()
    {
        var sink = new CollectingSink();
        var dispatcher = Build(services =>
        {
            _ = services.AddSingleton(sink);
            _ = services.AddGoalEventSink<CollectingSink>(new GoalEventSinkRegistration(new ComponentId("other"), 1, GoalEventDelivery.Observational, ServiceLifetime.Singleton, [new GoalProfileKey("other")]));
        });

        await dispatcher.PublishAsync(Event(), TestContext.Current.CancellationToken);

        sink.Seen.ShouldBeEmpty();
    }

    [Fact]
    public async Task PublishAsync_WhenObservationalSinkFails_SwallowsAndContinuesToLaterSinks()
    {
        var later = new CollectingSink();
        var dispatcher = Build(services =>
        {
            _ = services.AddGoalEventSink<FailingSink>(new GoalEventSinkRegistration(new ComponentId("failing"), 1, GoalEventDelivery.Observational, ServiceLifetime.Transient));
            _ = services.AddGoalEventSink<CollectingSink>(new GoalEventSinkRegistration(new ComponentId("later"), 2, GoalEventDelivery.Observational, ServiceLifetime.Singleton));
            _ = services.AddSingleton(later);
        });

        await dispatcher.PublishAsync(Event(), TestContext.Current.CancellationToken);

        later.Seen.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PublishAsync_WhenRequiredSinkFails_ThrowsAfterCommit()
    {
        var dispatcher = Build(static services =>
            services.AddGoalEventSink<FailingSink>(new GoalEventSinkRegistration(new ComponentId("audit"), 1, GoalEventDelivery.Required, ServiceLifetime.Transient)));

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await dispatcher.PublishAsync(Event(), TestContext.Current.CancellationToken));

        _ = exception.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task PublishAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        var dispatcher = Build(static _ => { });

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await dispatcher.PublishAsync(Event(), cancel.Token));
    }
}
