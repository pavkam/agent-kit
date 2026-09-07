// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddReadTool_WhenCalled_RegistersReadFileTool()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());

        _ = services.AddReadTool();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetServices<ITool>().ShouldHaveSingleItem().ShouldBeOfType<ReadFileTool>();
    }

    [Fact]
    public void AddReadTool_WhenCalledTwice_RegistersReadFileToolOnce()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());

        _ = services.AddReadTool();
        _ = services.AddReadTool();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetServices<ITool>().ShouldHaveSingleItem().ShouldBeOfType<ReadFileTool>();
    }

    [Fact]
    public void AddReadTool_WhenAnotherToolIsRegistered_PreservesBothRegistrations()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());
        _ = services.AddSingleton<ITool, StubTool>();

        _ = services.AddReadTool();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<ITool>()
            .Select(static tool => tool.Descriptor.Id)
            .ShouldBe([new ToolId("stub"), ReadFileTool.Id], ignoreOrder: true);
    }

    [Fact]
    public void AddReadTool_WhenComposedWithAgentTools_ResolvesThroughCatalog()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());
        _ = services.AddAgentTools(o => _ = o.AllowedToolIds.Add(ReadFileTool.Id));
        _ = services.AddReadTool();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IToolCatalog>().TryResolve(ReadFileTool.Id, out _).ShouldBeTrue();
    }
}
