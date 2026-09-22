// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddWriteTool_WhenServicesNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(services.AddWriteTool);

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    [Obsolete]
    public void AddWriteTool_WhenCalled_RegistersWriteFileTool()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());
        _ = TestFactory.AddSecurityDependencies(services);

        _ = services.AddWriteTool();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetServices<ITool>().ShouldHaveSingleItem().ShouldBeOfType<WriteFileTool>();
    }

    [Fact]
    [Obsolete]
    public void AddWriteTool_WhenCalledTwice_RegistersWriteFileToolOnce()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());
        _ = TestFactory.AddSecurityDependencies(services);

        _ = services.AddWriteTool();
        _ = services.AddWriteTool();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetServices<ITool>().ShouldHaveSingleItem().ShouldBeOfType<WriteFileTool>();
    }

    [Fact]
    [Obsolete]
    public void AddWriteTool_WhenAnotherToolIsRegistered_PreservesBothRegistrations()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());
        _ = TestFactory.AddSecurityDependencies(services);
        _ = services.AddSingleton<ITool, StubTool>();

        _ = services.AddWriteTool();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<ITool>()
            .Select(static tool => tool.Descriptor.Id)
            .ShouldBe([new ToolId("stub"), WriteFileTool.Id], ignoreOrder: true);
    }

    [Fact]
    [Obsolete]
    public void AddWriteTool_WhenComposedWithAgentTools_ResolvesThroughCatalog()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());
        _ = TestFactory.AddSecurityDependencies(services);
        _ = services.AddAgentTools(o => _ = o.AllowedToolIds.Add(WriteFileTool.Id));
        _ = services.AddWriteTool();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IToolCatalog>().TryResolve(WriteFileTool.Id, out _).ShouldBeTrue();
    }
}
