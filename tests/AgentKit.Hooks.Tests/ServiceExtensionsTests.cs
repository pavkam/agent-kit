// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentHooks_WhenCalled_RegistersDefaultDispatcher()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentHooks();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IHookDispatcher>().ShouldBeOfType<DefaultHookDispatcher>();
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
}
