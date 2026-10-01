// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting.Tests;

using AgentKit.Goals.Tests;

public sealed class ServiceExtensionsTests
{
    private sealed class OtherRunner: IDelegationChildRunner
    {
        public ValueTask<SessionId?> ProvisionSessionAsync(DelegationRequest delegation, GoalId childGoalId, int attemptNumber, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SessionId?>(null);

        public ValueTask<DelegationChildRunResult> RunAsync(DelegationChildRunRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static ServiceCollection Compose(Action<GoalWorkerOptions>? configure = null)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        var grants = new TestGoalGrants();
        _ = services.AddSingleton<ISecurityGrantStore>(grants);
        _ = services.AddSingleton<ISecurityAuthoritySelector>(new FixedSecurityAuthoritySelector(new DelegationHarness.GrantingAuthority(grants)));
        _ = services.AddAgentGoals();
        _ = services.AddGoalDelegationWorker(configure);
        return services;
    }

    [Fact]
    public void AddGoalDelegationWorker_WhenServicesIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddGoalDelegationWorker()).ParamName.ShouldBe("services");

    [Fact]
    public void ReplaceDelegationChildRunner_WhenServicesIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).ReplaceDelegationChildRunner<OtherRunner>()).ParamName.ShouldBe("services");

    [Fact]
    public void AddGoalDelegationWorker_WhenCalledTwice_RegistersTheWorkerAsOneHostedService()
    {
        var services = Compose();

        _ = services.AddGoalDelegationWorker(static options => options.MaximumConcurrentChildren = 2);

        services.Count(static descriptor => descriptor.ServiceType == typeof(GoalDelegationWorker)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(IHostedService)).ShouldBe(1);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<GoalWorkerOptions>>().Value.MaximumConcurrentChildren.ShouldBe(2);
    }

    [Fact]
    public void AddGoalDelegationWorker_WhenRegistered_ReplacesSignalAndParkingWithTheWorkersInstances()
    {
        using var provider = Compose().BuildServiceProvider();

        provider.GetRequiredService<IDelegationIntentSignal>().ShouldBeSameAs(provider.GetRequiredService<DelegationIntentQueue>());
        provider.GetRequiredService<IDelegationWaitParking>().ShouldBeSameAs(provider.GetRequiredService<DelegationWorkerSlots>());
        _ = provider.GetServices<IHostedService>().ShouldHaveSingleItem().ShouldBeOfType<GoalDelegationWorker>();
    }

    [Fact]
    public void AddGoalDelegationWorker_WhenRegistered_SelectsTheEngineRunnerAndAStoreIsNeverRegistered()
    {
        var services = Compose();

        services.Count(static descriptor => descriptor.ServiceType == typeof(IGoalStore)).ShouldBe(0);
        using var provider = services.BuildServiceProvider();
        _ = provider.GetServices<IDelegationChildRunner>().ShouldHaveSingleItem();
        services.Single(static descriptor => descriptor.ServiceType == typeof(IDelegationChildRunner)).ImplementationType.ShouldBe(typeof(EngineDelegationChildRunner));
    }

    [Theory]
    [InlineData(0, 1, 1, 1, 1)]
    [InlineData(1, 0, 1, 1, 1)]
    [InlineData(1, 1, 0, 1, 1)]
    [InlineData(1, 1, 1, 0, 1)]
    [InlineData(1, 1, 1, 1, 0)]
    public void AddGoalDelegationWorker_WhenABoundIsNotPositive_FailsWhenOptionsAreResolved(int slots, int queue, int scanSeconds, int page, int summary)
    {
        using var provider = Compose(options =>
        {
            options.MaximumConcurrentChildren = slots;
            options.QueueCapacity = queue;
            options.ScanInterval = TimeSpan.FromSeconds(scanSeconds);
            options.ScanPageSize = page;
            options.MaximumSummaryCharacters = summary;
        }).BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GoalWorkerOptions>>().Value);
    }

    [Fact]
    public void AddGoalDelegationWorker_WhenScannerIdIsBlank_FailsWhenOptionsAreResolved()
    {
        using var provider = Compose(static options => options.ScannerId = default).BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GoalWorkerOptions>>().Value);
    }

    [Fact]
    public void ReplaceDelegationChildRunner_WhenReplaced_ResolvesTheReplacement()
    {
        var services = Compose();
        _ = services.ReplaceDelegationChildRunner<OtherRunner>();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IDelegationChildRunner>().ShouldBeOfType<OtherRunner>();
    }

    [Fact]
    public void GoalWorkerOptions_WhenCreated_HasDocumentedDefaults()
    {
        var options = new GoalWorkerOptions();

        options.MaximumConcurrentChildren.ShouldBe(4);
        options.QueueCapacity.ShouldBe(256);
        options.ScanInterval.ShouldBe(TimeSpan.FromSeconds(30));
        options.ScanPageSize.ShouldBe(50);
        options.MaximumSummaryCharacters.ShouldBe(16_000);
        options.Profiles.ShouldBeEmpty();
        options.ScannerId.Value.ShouldBe("agentkit.goals.worker");
    }
}
