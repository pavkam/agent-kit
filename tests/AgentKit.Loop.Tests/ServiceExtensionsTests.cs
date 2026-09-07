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
        _ = services.AddSingleton<IContextAssembler, DefaultContextAssembler>();
        _ = services.AddSingleton<IToolInvoker>(new FakeToolInvoker(_ => TestFactory.SuccessResult()));
        _ = services.AddSingleton<IChatModel>(new FakeChatModel(new ModelAlias("chat")));
        return services;
    }
}
