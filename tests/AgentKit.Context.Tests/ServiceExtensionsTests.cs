// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentContext_WhenCalled_RegistersDefaultContextAssembler()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentContext();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IContextAssembler>().ShouldBeOfType<DefaultContextAssembler>();
    }

    [Fact]
    public void AddAgentContext_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentContext();
        _ = services.AddAgentContext();

        using var provider = services.BuildServiceProvider();
        provider.GetServices<IContextAssembler>().Count().ShouldBe(1);
    }
}
