// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddReadTool_WhenServicesNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(() => services.AddReadTool());

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    [Obsolete]
    public void AddReadTool_WhenConfigureProvided_AppliesOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());
        _ = TestFactory.AddSecurityDependencies(services);

        _ = services.AddReadTool(static options =>
        {
            options.DefaultMaximumLines = 500;
            options.MaximumLines = 5_000;
        });
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ReadFileToolOptions>>().Value;
        options.DefaultMaximumLines.ShouldBe(500);
        options.MaximumLines.ShouldBe(5_000);
        _ = provider.GetServices<ITool>().ShouldHaveSingleItem().ShouldBeOfType<ReadFileTool>();
    }

    [Fact]
    [Obsolete]
    public void AddReadTool_WhenOptionsInvalid_FailsValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());
        _ = TestFactory.AddSecurityDependencies(services);

        _ = services.AddReadTool(static options => options.DefaultMaximumLines = options.MaximumLines + 1);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ReadFileToolOptions>>().Value);
    }

    [Fact]
    [Obsolete]
    public void AddReadTool_WhenCalled_RegistersReadFileTool()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());
        _ = TestFactory.AddSecurityDependencies(services);

        _ = services.AddReadTool();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetServices<ITool>().ShouldHaveSingleItem().ShouldBeOfType<ReadFileTool>();
    }

    [Fact]
    [Obsolete]
    public void AddReadTool_WhenCalledTwice_RegistersReadFileToolOnce()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());
        _ = TestFactory.AddSecurityDependencies(services);

        _ = services.AddReadTool();
        _ = services.AddReadTool();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetServices<ITool>().ShouldHaveSingleItem().ShouldBeOfType<ReadFileTool>();
    }

    [Fact]
    [Obsolete]
    public void AddReadTool_WhenAnotherToolIsRegistered_PreservesBothRegistrations()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());
        _ = TestFactory.AddSecurityDependencies(services);
        _ = services.AddSingleton<ITool, StubTool>();

        _ = services.AddReadTool();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<ITool>()
            .Select(static tool => tool.Descriptor.Id)
            .ShouldBe([new ToolId("stub"), ReadFileTool.Id], ignoreOrder: true);
    }

    [Fact]
    [Obsolete]
    public void AddReadTool_WhenComposedWithAgentTools_ResolvesThroughCatalog()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileSystem>(new FakeFileSystem());
        _ = TestFactory.AddSecurityDependencies(services);
        _ = services.AddAgentTools(o => _ = o.AllowedToolIds.Add(ReadFileTool.Id));
        _ = services.AddReadTool();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IToolCatalog>().TryResolve(ReadFileTool.Id, out _).ShouldBeTrue();
    }
}
