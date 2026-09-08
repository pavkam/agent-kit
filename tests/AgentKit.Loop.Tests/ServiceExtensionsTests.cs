// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentLoop_WhenCalled_RegistersDefaultAgentLoop()
    {
        var services = BuildComposableServices();

        _ = services.AddAgentLoop();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IAgentLoop>().ShouldBeOfType<DefaultAgentLoop>();
    }

    [Fact]
    public void AddAgentLoop_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = BuildComposableServices();

        _ = services.AddAgentLoop();
        _ = services.AddAgentLoop();

        using var provider = services.BuildServiceProvider();
        provider.GetServices<IAgentLoop>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddAgentLoop_WhenCalled_RegistersOnlyKeyedDefaultContinuationPolicy()
    {
        var services = BuildComposableServices();
        _ = services.AddAgentLoop();

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
    public void AddAgentLoop_WhenHistoryReadPageSizeIsNotPositive_FailsValidationOnAccess()
    {
        var services = BuildComposableServices();

        _ = services.AddAgentLoop(static options => options.HistoryReadPageSize = 0);

        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(provider.GetRequiredService<IAgentLoop>);
    }

    private static ServiceCollection BuildComposableServices()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISessionCoordinator>(new FakeSessionCoordinator(new BranchId(Guid.NewGuid())));
        _ = services.AddSingleton<ISecurityProfileSelector, FakeSecurityProfileSelector>();
        _ = services.AddSingleton<IContextAssembler, DefaultContextAssembler>();
        _ = services.AddSingleton<IToolInvoker>(new FakeToolInvoker(_ => TestFactory.SuccessResult()));

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
}
