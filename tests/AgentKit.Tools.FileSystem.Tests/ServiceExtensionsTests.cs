// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.FileSystem.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddFileSystemTools_WhenCalled_RegistersBothTools()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());

        _ = services.AddFileSystemTools();
        using var provider = services.BuildServiceProvider();

        var tools = provider.GetServices<ITool>().ToImmutableArray();
        tools.Select(static t => t.Descriptor.Id).ShouldBe([ReadFileTool.Id, WriteFileTool.Id], ignoreOrder: true);
    }

    [Fact]
    public void AddFileSystemTools_WhenComposedWithAgentTools_ResolvesThroughCatalog()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());
        _ = services.AddAgentTools(o =>
        {
            _ = o.AllowedToolIds.Add(ReadFileTool.Id);
            _ = o.AllowedToolIds.Add(WriteFileTool.Id);
        });
        _ = services.AddFileSystemTools();
        using var provider = services.BuildServiceProvider();

        var catalog = provider.GetRequiredService<IToolCatalog>();
        catalog.TryResolve(ReadFileTool.Id, out _).ShouldBeTrue();
        catalog.TryResolve(WriteFileTool.Id, out _).ShouldBeTrue();
    }
}
