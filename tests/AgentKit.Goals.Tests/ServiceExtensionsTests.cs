// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

using AgentKit.Goals.InMemory;

/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    private sealed class StubDispatcher: IDelegationDispatcher
    {
        public DelegationDispatcherDescriptor Descriptor { get; } = new(new ComponentId("stub"), isSingletonSafe: true, isDurable: false);

        public Task<DelegationResult> DispatchAsync(AuthorizedDelegation request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class OtherStubDispatcher: IDelegationDispatcher
    {
        public DelegationDispatcherDescriptor Descriptor { get; } = new(new ComponentId("other"), isSingletonSafe: true, isDurable: false);

        public Task<DelegationResult> DispatchAsync(AuthorizedDelegation request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubJoin: IGoalJoinStrategy
    {
        public GoalJoinStrategyKey Key { get; } = new("stub.join");

        public ValueTask<GoalJoinDecision> EvaluateAsync(GoalJoinEvaluationRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<GoalJoinDecision>(new GoalJoinUnsatisfiable("stub"));
    }

    private sealed class StubSignal: IDelegationIntentSignal
    {
        public ValueTask SignalAsync(DelegationIntent intent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    [Fact]
    public void AddAgentGoals_WhenServicesIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddAgentGoals()).ParamName.ShouldBe("services");

    [Fact]
    public void AddAgentGoals_WhenCalledTwice_RegistersEachSingularServiceOnceAndAddsNoStore()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentGoals().AddAgentGoals();

        services.Count(static descriptor => descriptor.ServiceType == typeof(IDelegationCoordinator)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(IGoalCoordinator)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(IGoalStore)).ShouldBe(0);
        services.Count(static descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(IGoalJoinStrategy)).ShouldBe(5);
    }

    [Fact]
    public void AddAgentGoals_WhenOptionsAreInvalid_FailsWhenOptionsAreResolved()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentGoals(static options => options.MaximumDelegationDepth = 0);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<Microsoft.Extensions.Options.OptionsValidationException>(() => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentGoalOptions>>().Value);
    }

    [Fact]
    public void AddDelegationDispatcher_WhenKeyRepeatsWithSameType_IsIdempotentAndWithDifferentTypeThrows()
    {
        var key = new DelegationDispatcherKey("d");
        var services = new ServiceCollection();
        _ = services.AddDelegationDispatcher<StubDispatcher>(key).AddDelegationDispatcher<StubDispatcher>(key);

        _ = Should.Throw<InvalidOperationException>(() => services.AddDelegationDispatcher<OtherStubDispatcher>(key));

        services.Count(static descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(IDelegationDispatcher)).ShouldBe(1);
    }

    [Fact]
    public void ReplaceDelegationDispatcher_WhenKeyExists_ReplacesItsRegistration()
    {
        var key = new DelegationDispatcherKey("d");
        var services = new ServiceCollection();
        _ = services.AddDelegationDispatcher<StubDispatcher>(key).ReplaceDelegationDispatcher<OtherStubDispatcher>(key);
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<IDelegationDispatcher>("d").ShouldBeOfType<OtherStubDispatcher>();
    }

    [Fact]
    public void AddDelegationDispatcher_WhenKeyIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddDelegationDispatcher<StubDispatcher>(default)).ParamName.ShouldBe("key");

    [Fact]
    public void AddGoalJoinStrategy_WhenKeyCollidesWithBuiltIn_Throws() =>
        Should.Throw<InvalidOperationException>(() => new ServiceCollection().AddGoalJoinStrategy<StubJoin>(GoalJoinStrategyKeys.All));

    [Fact]
    public void AddGoalJoinStrategy_WhenKeyIsNew_ResolvesThroughKeyedRegistration()
    {
        var services = new ServiceCollection();
        _ = services.AddGoalJoinStrategy<StubJoin>(new GoalJoinStrategyKey("stub.join"));
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<IGoalJoinStrategy>("stub.join").ShouldBeOfType<StubJoin>();
    }

    [Fact]
    public void ReplaceGoalJoinStrategy_WhenBuiltInKeyIsReplaced_UsesReplacement()
    {
        var services = new ServiceCollection();
        _ = services.ReplaceGoalJoinStrategy<StubJoin>(GoalJoinStrategyKeys.All);
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<IGoalJoinStrategy>(GoalJoinStrategyKeys.All.Value).ShouldBeOfType<StubJoin>();
    }

    [Fact]
    public void ReplaceDelegationIntentSignal_WhenReplaced_ResolvesReplacementAndDefaultDiscardsSignals()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentGoals();
        using (var provider = services.BuildServiceProvider())
        {
            _ = provider.GetRequiredService<IDelegationIntentSignal>().ShouldBeOfType<NoOpDelegationIntentSignal>();
        }

        _ = services.ReplaceDelegationIntentSignal<StubSignal>();
        using var replaced = services.BuildServiceProvider();

        _ = replaced.GetRequiredService<IDelegationIntentSignal>().ShouldBeOfType<StubSignal>();
    }

    [Fact]
    public void AddGoalEventSink_WhenSameIdentityRegisteredDifferently_Throws()
    {
        var services = new ServiceCollection();
        _ = services.AddGoalEventSink<NullSink>(new GoalEventSinkRegistration(new ComponentId("s"), 1, GoalEventDelivery.Observational, ServiceLifetime.Singleton));

        _ = services.AddGoalEventSink<NullSink>(new GoalEventSinkRegistration(new ComponentId("s"), 1, GoalEventDelivery.Observational, ServiceLifetime.Singleton));
        _ = Should.Throw<InvalidOperationException>(() =>
            services.AddGoalEventSink<NullSink>(new GoalEventSinkRegistration(new ComponentId("s"), 2, GoalEventDelivery.Required, ServiceLifetime.Singleton)));
    }

    [Fact]
    public void AddLocalDelegationDispatcher_WhenRegistered_ResolvesLocalDispatcherThatClaimsNoDurability()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        _ = services.AddSingleton<ISecurityGrantStore>(new TestGoalGrants());
        _ = services.AddSingleton<ISecurityAuthoritySelector>(new FixedSecurityAuthoritySelector(new DelegationHarness.GrantingAuthority(new TestGoalGrants())));
        _ = services.AddAgentGoals().AddInMemoryGoalStore(new GoalStoreKey("m")).AddLocalDelegationDispatcher(new DelegationDispatcherKey("local"));
        using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredKeyedService<IDelegationDispatcher>("local");

        dispatcher.Descriptor.IsDurable.ShouldBeFalse();
        dispatcher.Descriptor.IsSingletonSafe.ShouldBeTrue();
    }

    private sealed class NullSink: IGoalEventSink
    {
        public ValueTask PublishAsync(GoalEvent goalEvent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
