// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddRunStartedHook_WhenCalled_RegistersTheHookAndTheDispatcherOnce()
    {
        var services = new ServiceCollection();
        var descriptor = HookRegistrationDescriptors.ForPoint(
            new HookId("test.run-started"),
            AgentHookPointDefinitions.RunStartedRegistration);

        _ = services.AddRunStartedHook<TestRunStartedHook>(descriptor).AddRunStartedHook<TestRunStartedHook>(descriptor);

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IHookDispatcher>().ShouldBeOfType<DefaultHookDispatcher>();
        _ = provider.GetServices<IRunStartedHook>().ShouldHaveSingleItem().ShouldBeOfType<TestRunStartedHook>();
    }

    [Fact]
    public void AddBeforeModelRequestHook_WhenCalled_RegistersAdditively()
    {
        var services = new ServiceCollection();

        _ = services.AddBeforeModelRequestHook<TestBeforeModelRequestHook>(
                HookRegistrationDescriptors.ForPoint(new HookId("test.before-model"), AgentHookPointDefinitions.BeforeModelRequestRegistration))
            .AddBeforeModelRequestHook<OtherBeforeModelRequestHook>(
                HookRegistrationDescriptors.ForPoint(new HookId("test.before-model.other"), AgentHookPointDefinitions.BeforeModelRequestRegistration));

        using var provider = services.BuildServiceProvider();
        provider.GetServices<IBeforeModelRequestHook>().Select(static h => h.GetType()).ShouldBe([typeof(TestBeforeModelRequestHook), typeof(OtherBeforeModelRequestHook)]);
    }

    [Fact]
    public void AddBeforeToolInvocationHook_WhenCalled_RegistersTheHook()
    {
        var services = new ServiceCollection();

        _ = services.AddBeforeToolInvocationHook<TestBeforeToolInvocationHook>(
            HookRegistrationDescriptors.ForPoint(new HookId("test.before-tool"), AgentHookPointDefinitions.BeforeToolInvocationRegistration));

        using var provider = services.BuildServiceProvider();
        _ = provider.GetServices<IBeforeToolInvocationHook>().ShouldHaveSingleItem().ShouldBeOfType<TestBeforeToolInvocationHook>();
    }

    [Fact]
    public void AddMemoryHooks_WhenCalled_RegisterEachHookAndPublishItsPointDefinition()
    {
        var services = new ServiceCollection();

        _ = services.AddBeforeMemoryProposalHook<TestBeforeMemoryProposalHook>(
            HookRegistrationDescriptors.ForPoint(new HookId("test.proposal"), AgentHookPointDefinitions.BeforeMemoryProposalRegistration));
        _ = services.AddBeforeMemoryWriteHook<TestBeforeMemoryWriteHook>(
            HookRegistrationDescriptors.ForPoint(new HookId("test.write"), AgentHookPointDefinitions.BeforeMemoryWriteRegistration));
        _ = services.AddBeforeRetrievalHook<TestBeforeRetrievalHook>(
            HookRegistrationDescriptors.ForPoint(new HookId("test.retrieval"), AgentHookPointDefinitions.BeforeRetrievalRegistration));
        _ = services.AddBeforeRetrievalExposureHook<TestBeforeRetrievalExposureHook>(
            HookRegistrationDescriptors.ForPoint(new HookId("test.exposure"), AgentHookPointDefinitions.BeforeRetrievalExposureRegistration));

        using var provider = services.BuildServiceProvider();
        _ = provider.GetServices<IBeforeMemoryProposalHook>().ShouldHaveSingleItem().ShouldBeOfType<TestBeforeMemoryProposalHook>();
        _ = provider.GetServices<IBeforeMemoryWriteHook>().ShouldHaveSingleItem().ShouldBeOfType<TestBeforeMemoryWriteHook>();
        _ = provider.GetServices<IBeforeRetrievalHook>().ShouldHaveSingleItem().ShouldBeOfType<TestBeforeRetrievalHook>();
        _ = provider.GetServices<IBeforeRetrievalExposureHook>().ShouldHaveSingleItem().ShouldBeOfType<TestBeforeRetrievalExposureHook>();
        provider.GetRequiredService<HookPointDefinition<IBeforeMemoryProposalHook, BeforeMemoryProposalEventArgs>>().Kind.ShouldBe(HookPointKind.ShortCircuiting);
        provider.GetRequiredService<HookPointDefinition<IBeforeMemoryWriteHook, BeforeMemoryWriteEventArgs>>().Kind.ShouldBe(HookPointKind.ShortCircuiting);
        provider.GetRequiredService<HookPointDefinition<IBeforeRetrievalHook, BeforeRetrievalEventArgs>>().Kind.ShouldBe(HookPointKind.Mutating);
        provider.GetRequiredService<HookPointDefinition<IBeforeRetrievalExposureHook, BeforeRetrievalExposureEventArgs>>().Kind.ShouldBe(HookPointKind.Mutating);
        provider.GetRequiredService<IReadOnlyList<HookPointDefinitionRegistration>>().Select(static registration => registration.Point)
            .ShouldContain(AgentHookPoints.BeforeMemoryProposal);
        provider.GetRequiredService<IReadOnlyList<HookPointDefinitionRegistration>>().Select(static registration => registration.Point)
            .ShouldContain(AgentHookPoints.BeforeRetrievalExposure);
    }

    [Fact]
    public void AddMemoryHooks_WhenDescriptorNamesAnotherPoint_ThrowsArgumentExceptionAndServicesNullThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var wrong = HookRegistrationDescriptors.ForPoint(new HookId("wrong"), AgentHookPointDefinitions.RunStartedRegistration);
        IServiceCollection missing = null!;

        _ = Should.Throw<ArgumentException>(() => services.AddBeforeMemoryProposalHook<TestBeforeMemoryProposalHook>(wrong));
        _ = Should.Throw<ArgumentException>(() => services.AddBeforeMemoryWriteHook<TestBeforeMemoryWriteHook>(wrong));
        _ = Should.Throw<ArgumentException>(() => services.AddBeforeRetrievalHook<TestBeforeRetrievalHook>(wrong));
        _ = Should.Throw<ArgumentException>(() => services.AddBeforeRetrievalExposureHook<TestBeforeRetrievalExposureHook>(wrong));
        Should.Throw<ArgumentNullException>(() => missing.AddBeforeMemoryProposalHook<TestBeforeMemoryProposalHook>(wrong)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => missing.AddBeforeMemoryWriteHook<TestBeforeMemoryWriteHook>(wrong)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => missing.AddBeforeRetrievalHook<TestBeforeRetrievalHook>(wrong)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => missing.AddBeforeRetrievalExposureHook<TestBeforeRetrievalExposureHook>(wrong)).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddPointHooks_WhenServicesNull_ThrowArgumentNullException()
    {
        IServiceCollection services = null!;
        var descriptor = HookRegistrationDescriptors.ForPoint(
            new HookId("test"),
            AgentHookPointDefinitions.RunStartedRegistration);

        Should.Throw<ArgumentNullException>(() => services.AddRunStartedHook<TestRunStartedHook>(descriptor)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.AddBeforeModelRequestHook<TestBeforeModelRequestHook>(descriptor)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.AddBeforeToolInvocationHook<TestBeforeToolInvocationHook>(descriptor)).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddAgentHooks_WhenServicesNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(() => services.AddAgentHooks());

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddAgentHooks_WhenCalled_RegistersDefaultDispatcher()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentHooks();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IHookDispatcher>().ShouldBeOfType<DefaultHookDispatcher>();
        _ = provider.GetRequiredService<IHookCatalog>().ShouldBeOfType<HookRegistrationCatalog>();
        _ = provider.GetRequiredService<IHookInstanceFactory>().ShouldBeOfType<ServiceProviderHookInstanceFactory>();
        _ = provider.GetRequiredService<IHookProfileSelector>().ShouldBeOfType<DefaultHookProfileSelector>();
        _ = provider.GetRequiredService<IHookDiagnosticDispatcher>().ShouldBeOfType<HookDiagnosticDispatcher>();
        _ = provider.GetRequiredService<IHookOrderResolver>().ShouldBeOfType<HookOrderResolver>();
        _ = provider.GetRequiredService<IIdentifierGenerator<HookDispatchId>>().ShouldNotBeNull();
        _ = provider.GetRequiredService<IIdentifierGenerator<HookInvocationId>>().ShouldNotBeNull();
    }

    [Fact]
    public void AddAgentHooks_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentHooks();
        _ = services.AddAgentHooks();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<IHookDispatcher>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddAgentHooks_WhenConfigureOmitted_UsesDocumentedDefaults()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentHooks();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<AgentHookOptions>>().Value;
        options.MaximumInvocationDepth.ShouldBe(8);
        options.MinimumFailureMode.ShouldBe(HookFailureMode.IsolateAndDiagnose);
    }

    [Fact]
    public void AddAgentHooks_WhenConfigureSupplied_AppliesConfiguration()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentHooks(static options =>
        {
            options.MaximumInvocationDepth = 3;
            options.MinimumFailureMode = HookFailureMode.FailOperation;
        });
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<AgentHookOptions>>().Value;
        options.MaximumInvocationDepth.ShouldBe(3);
        options.MinimumFailureMode.ShouldBe(HookFailureMode.FailOperation);
    }

    [Fact]
    public void AddAgentHooks_WhenMaximumInvocationDepthIsZero_FailsOptionsValidation()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentHooks(static options => options.MaximumInvocationDepth = 0);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AgentHookOptions>>().Value);
        _ = Should.Throw<OptionsValidationException>(provider.GetRequiredService<IHookDispatcher>);
    }

    [Fact]
    public void AddAgentHooks_WhenMinimumFailureModeUndefined_FailsOptionsValidation()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentHooks(static options => options.MinimumFailureMode = (HookFailureMode) 42);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AgentHookOptions>>().Value);
        _ = Should.Throw<OptionsValidationException>(provider.GetRequiredService<IHookDispatcher>);
    }

    [Fact]
    public async Task AddHookProfile_WhenNamedProfileRegistered_SelectorResolvesIt()
    {
        var services = new ServiceCollection();
        var minimal = new HookProfileKey("minimal");
        _ = services.AddAgentHooks().AddHookProfile(minimal, static _ => { });
        await using var provider = services.BuildServiceProvider();

        var selection = await provider.GetRequiredService<IHookProfileSelector>()
            .SelectAsync(new HookProfileSelectionRequest(minimal, null), TestContext.Current.CancellationToken);

        selection.ShouldBeOfType<HookProfileSelected>().ProfileKey.ShouldBe(minimal);
    }

    private sealed class TestRunStartedHook: IRunStartedHook
    {
        public ValueTask InvokeAsync(RunStartedEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class TestBeforeModelRequestHook: IBeforeModelRequestHook
    {
        public ValueTask InvokeAsync(BeforeModelRequestEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class OtherBeforeModelRequestHook: IBeforeModelRequestHook
    {
        public ValueTask InvokeAsync(BeforeModelRequestEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class TestBeforeToolInvocationHook: IBeforeToolInvocationHook
    {
        public ValueTask InvokeAsync(BeforeToolInvocationEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class TestBeforeMemoryProposalHook: IBeforeMemoryProposalHook
    {
        public ValueTask InvokeAsync(BeforeMemoryProposalEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class TestBeforeMemoryWriteHook: IBeforeMemoryWriteHook
    {
        public ValueTask InvokeAsync(BeforeMemoryWriteEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class TestBeforeRetrievalHook: IBeforeRetrievalHook
    {
        public ValueTask InvokeAsync(BeforeRetrievalEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class TestBeforeRetrievalExposureHook: IBeforeRetrievalExposureHook
    {
        public ValueTask InvokeAsync(BeforeRetrievalExposureEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }
}
