// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentTools_WhenCalled_RegistersCatalogAuthorizerAndInvoker()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentTools();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IToolCatalog>().ShouldBeOfType<ToolCatalog>();
        _ = provider.GetRequiredService<IToolAuthorizer>().ShouldBeOfType<AllowListToolAuthorizer>();
        _ = provider.GetRequiredService<IToolInvoker>().ShouldBeOfType<DefaultToolInvoker>();
    }

    [Fact]
    public void AddAgentTools_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentTools();
        _ = services.AddAgentTools();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<IToolInvoker>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddAgentTools_WhenConfigureProvided_AppliesOptions()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentTools(o => o.AllowedToolIds.Add(new ToolId("configured")));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<AgentToolsOptions>>().Value.AllowedToolIds
            .ShouldContain(new ToolId("configured"));
    }

    [Fact]
    public void AddTool_WhenCalledForMultipleTools_RegistersEachAdditively()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentTools();
        _ = services.AddTool<AlphaTool>();
        _ = services.AddTool<BetaTool>();
        using var provider = services.BuildServiceProvider();

        var catalog = provider.GetRequiredService<IToolCatalog>();
        catalog.Descriptors.Select(static d => d.Id).ShouldBe([new ToolId("alpha"), new ToolId("beta")], ignoreOrder: true);
    }

    private sealed class AlphaTool: ITool
    {
        public ToolDescriptor Descriptor { get; } = TestFactory.Descriptor("alpha");

        public Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class BetaTool: ITool
    {
        public ToolDescriptor Descriptor { get; } = TestFactory.Descriptor("beta");

        public Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
