// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

public sealed class ServiceExtensionsTests
{
    private static readonly ComponentKey<IAgentLoop> LoopKey = new("test-loop");

    [Fact]
    public void AddAgentLoop_WhenCalled_RegistersKeyedDefaultAgentLoop()
    {
        var services = BuildComposableServices();

        _ = services.AddAgentLoop(LoopKey);

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredKeyedService<IAgentLoop>(LoopKey.Value).ShouldBeOfType<DefaultAgentLoop>();
    }

    [Fact]
    public void AddAgentLoop_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = BuildComposableServices();

        _ = services.AddAgentLoop(LoopKey);
        _ = services.AddAgentLoop(LoopKey);

        using var provider = services.BuildServiceProvider();
        provider.GetKeyedServices<IAgentLoop>(LoopKey.Value).Count().ShouldBe(1);
    }

    [Fact]
    public void AddAgentLoop_WhenAConflictingImplementationIsAlreadyRegisteredUnderTheSameKey_ThrowsInvalidOperationException()
    {
        var services = BuildComposableServices();
        _ = services.AddAgentLoop<ScriptedRunContinuationPolicyHostLoop>(LoopKey);

        var exception = Should.Throw<InvalidOperationException>(() => services.AddAgentLoop(LoopKey));

        exception.Message.ShouldContain(LoopKey.Value);
    }

    [Fact]
    public void ReplaceAgentLoop_WhenCalled_ReplacesTheExistingKeyedRegistration()
    {
        var services = BuildComposableServices();
        _ = services.AddAgentLoop(LoopKey);

        _ = services.ReplaceAgentLoop<ScriptedRunContinuationPolicyHostLoop>(LoopKey);

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredKeyedService<IAgentLoop>(LoopKey.Value)
            .ShouldBeOfType<ScriptedRunContinuationPolicyHostLoop>();
    }

    [Fact]
    public void AddAgentLoop_WhenTwoKeysAreRegistered_ResolvesEachLoopIndependently()
    {
        var services = BuildComposableServices();
        var otherKey = new ComponentKey<IAgentLoop>("other-loop");
        _ = services.AddAgentLoop(LoopKey);
        _ = services.AddAgentLoop<ScriptedRunContinuationPolicyHostLoop>(otherKey);

        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<IAgentLoop>(LoopKey.Value).ShouldBeOfType<DefaultAgentLoop>();
        _ = provider.GetRequiredKeyedService<IAgentLoop>(otherKey.Value)
            .ShouldBeOfType<ScriptedRunContinuationPolicyHostLoop>();
    }

    [Fact]
    public void AddAgentLoop_WhenCalled_RegistersOnlyKeyedDefaultContinuationPolicy()
    {
        var services = BuildComposableServices();
        _ = services.AddAgentLoop(LoopKey);

        using var provider = services.BuildServiceProvider();

        provider.GetService<IRunContinuationPolicy>().ShouldBeNull();
        _ = provider.GetRequiredKeyedService<IRunContinuationPolicy>(
            AgentLoopDefaults.ContinuationPolicyKey.Value).ShouldBeOfType<DefaultRunContinuationPolicy>();
    }

    [Fact]
    public void AddRunContinuationPolicy_WhenKeysDiffer_ResolvesEachPolicyIndependently()
    {
        var services = new ServiceCollection();
        _ = services.AddRunContinuationPolicy<TestContinuationPolicy>(new ComponentKey<IRunContinuationPolicy>("one"));
        _ = services.AddRunContinuationPolicy<OtherContinuationPolicy>(new ComponentKey<IRunContinuationPolicy>("two"));

        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<IRunContinuationPolicy>("one").ShouldBeOfType<TestContinuationPolicy>();
        _ = provider.GetRequiredKeyedService<IRunContinuationPolicy>("two").ShouldBeOfType<OtherContinuationPolicy>();
    }

    [Fact]
    public void AddRunContinuationPolicy_WhenKeyRepeats_PreservesBothDescriptors()
    {
        var services = new ServiceCollection();
        var key = new ComponentKey<IRunContinuationPolicy>("duplicate");
        _ = services.AddRunContinuationPolicy<TestContinuationPolicy>(key);
        _ = services.AddRunContinuationPolicy<OtherContinuationPolicy>(key);

        services.Count(descriptor => descriptor.ServiceKey?.Equals("duplicate") == true).ShouldBe(2);
    }

    [Fact]
    public void AddRunContinuationPolicy_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(() =>
            services.AddRunContinuationPolicy<TestContinuationPolicy>(new ComponentKey<IRunContinuationPolicy>("test")));

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddAgentLoop_WhenCalled_ResolvesDefaultAgentLoopWithTheKeyedContinuationPolicy()
    {
        var services = BuildComposableServices();
        var policy = new ScriptedRunContinuationPolicy(static _ => throw new NotSupportedException());
        _ = services.AddKeyedSingleton<IRunContinuationPolicy>(AgentLoopDefaults.ContinuationPolicyKey.Value, policy);

        _ = services.AddAgentLoop(LoopKey);

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredKeyedService<IAgentLoop>(LoopKey.Value).ShouldBeOfType<DefaultAgentLoop>();
        provider.GetRequiredKeyedService<IRunContinuationPolicy>(AgentLoopDefaults.ContinuationPolicyKey.Value)
            .ShouldBeSameAs(policy);
    }

    [Fact]
    public void AddAgentLoop_WhenNoPolicyIsRegisteredUnderTheDefaultKey_ResolvesTheBuiltInPolicyForTheLoop()
    {
        var services = BuildComposableServices();

        _ = services.AddAgentLoop(LoopKey);

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredKeyedService<IAgentLoop>(LoopKey.Value).ShouldBeOfType<DefaultAgentLoop>();
        _ = provider.GetRequiredKeyedService<IRunContinuationPolicy>(AgentLoopDefaults.ContinuationPolicyKeyValue)
            .ShouldBeOfType<DefaultRunContinuationPolicy>();
        AgentLoopDefaults.ContinuationPolicyKey.Value.ShouldBe(AgentLoopDefaults.ContinuationPolicyKeyValue);
    }

    [Fact]
    public void AddAgentLoop_WhenHistoryReadPageSizeIsNotPositive_FailsValidationOnAccess()
    {
        var services = BuildComposableServices();

        _ = services.AddAgentLoop(LoopKey, static options => options.HistoryReadPageSize = 0);

        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredKeyedService<IAgentLoop>(LoopKey.Value));
    }

    [Fact]
    public void AddAgentLoop_WhenAppendConflictRetryLimitIsNegative_FailsValidationOnAccess()
    {
        var services = BuildComposableServices();

        _ = services.AddAgentLoop(LoopKey, static options => options.AppendConflictRetryLimit = -1);

        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredKeyedService<IAgentLoop>(LoopKey.Value));
    }

    [Fact]
    public void AddAgentLoop_WhenAppendConflictRetryLimitIsZero_PassesValidation()
    {
        var services = BuildComposableServices();

        _ = services.AddAgentLoop(LoopKey, static options => options.AppendConflictRetryLimit = 0);

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredKeyedService<IAgentLoop>(LoopKey.Value).ShouldBeOfType<DefaultAgentLoop>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddAgentLoop_WhenSettlementTimeoutIsNotPositive_FailsValidationOnAccess(int seconds)
    {
        var services = BuildComposableServices();

        _ = services.AddAgentLoop(LoopKey, options => options.SettlementTimeout = TimeSpan.FromSeconds(seconds));

        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredKeyedService<IAgentLoop>(LoopKey.Value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddAgentLoop_WhenObserverDeliveryTimeoutIsNotPositive_FailsValidationOnAccess(int seconds)
    {
        var services = BuildComposableServices();

        _ = services.AddAgentLoop(LoopKey, options => options.ObserverDeliveryTimeout = TimeSpan.FromSeconds(seconds));

        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredKeyedService<IAgentLoop>(LoopKey.Value));
    }

    [Fact]
    public void AddAgentLoop_WhenOptionsAreDefault_UsesDocumentedDefaults()
    {
        var options = new AgentLoopOptions();

        options.HistoryReadPageSize.ShouldBe(200);
        options.AppendConflictRetryLimit.ShouldBe(5);
        options.SettlementTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        options.ObserverDeliveryTimeout.ShouldBe(TimeSpan.FromSeconds(5));
    }

    private static ServiceCollection BuildComposableServices()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISessionCoordinator>(new FakeSessionCoordinator(new BranchId(Guid.NewGuid())));
        _ = services.AddSingleton<ISecurityProfileSelector, FakeSecurityProfileSelector>();
        _ = services.AddAgentContext();
        _ = services.AddSingleton<ILegacyToolCallOrchestrator>(new FakeToolInvoker(_ => TestFactory.SuccessResult()));

        // The loop no longer owns model selection, so a composable graph must
        // supply the provider-runtime collaborators separately. AddAgentLoop
        // deliberately does not register them.
        var descriptor = TestFactory.Model();
        _ = services.AddSingleton<IModelCatalog>(
            new FakeModelCatalog(TestFactory.Catalog(descriptor)));
        _ = services.AddSingleton<IModelSelector>(FakeModelSelector.Selecting(descriptor));
        _ = services.AddSingleton<ILlmModelResolver>(
            new FakeLlmModelResolver(new FakeLlmModel(new ModelAlias("chat"))));
        return services;
    }

    private sealed class TestContinuationPolicy: IRunContinuationPolicy
    {
        public ValueTask<RunContinuationDecision> DecideAsync(
            RunContinuationContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class OtherContinuationPolicy: IRunContinuationPolicy
    {
        public ValueTask<RunContinuationDecision> DecideAsync(
            RunContinuationContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>A minimal alternate <see cref="IAgentLoop"/> used only to prove replacement and conflict diagnosis.</summary>
    private sealed class ScriptedRunContinuationPolicyHostLoop: IAgentLoop
    {
        public Task<AgentLoopResult> RunAsync(
            AgentLoopRunRequest request,
            AgentRunServices services,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
