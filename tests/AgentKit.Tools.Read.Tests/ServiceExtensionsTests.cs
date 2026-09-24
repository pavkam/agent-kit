// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read.Tests;

using AgentKit.Tools;

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
    public void AddReadTool_WhenConfigureProvided_AppliesOptions()
    {
        var services = new ServiceCollection();
        _ = TestFactory.AddToolDependencies(services);

        _ = services.AddReadTool(static options =>
        {
            options.HostRootPath = Path.GetTempPath();
            options.DefaultMaximumLines = 500;
            options.MaximumLines = 5_000;
        });
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ReadFileToolOptions>>().Value;
        options.DefaultMaximumLines.ShouldBe(500);
        options.MaximumLines.ShouldBe(5_000);
        provider.GetServices<RegisteredToolInvoker>().ShouldHaveSingleItem().Descriptor.Id.ShouldBe(ReadFileTool.Id);
    }

    [Fact]
    public void AddReadTool_WhenOptionsInvalid_FailsValidation()
    {
        var services = new ServiceCollection();
        _ = TestFactory.AddToolDependencies(services);

        _ = services.AddReadTool(static options =>
        {
            options.HostRootPath = Path.GetTempPath();
            options.DefaultMaximumLines = options.MaximumLines + 1;
        });
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ReadFileToolOptions>>().Value);
    }

    [Fact]
    public void AddReadTool_WhenHostRootMissing_FailsValidation()
    {
        var services = new ServiceCollection();
        _ = TestFactory.AddToolDependencies(services);
        _ = services.AddReadTool();
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ReadFileToolOptions>>().Value);
    }

    [Fact]
    public void AddReadTool_WhenCalled_RegistersReadFileTool()
    {
        var services = new ServiceCollection();
        _ = TestFactory.AddToolDependencies(services);

        _ = services.AddReadTool(static options => options.HostRootPath = Path.GetTempPath());
        using var provider = services.BuildServiceProvider();

        provider.GetServices<RegisteredToolInvoker>().ShouldHaveSingleItem().Descriptor.Id.ShouldBe(ReadFileTool.Id);
    }

    [Fact]
    public void AddReadTool_WhenCalledTwice_RegistersReadFileToolOnce()
    {
        var services = new ServiceCollection();
        _ = TestFactory.AddToolDependencies(services);

        _ = services.AddReadTool(static options => options.HostRootPath = Path.GetTempPath());
        _ = services.AddReadTool(static options => options.HostRootPath = Path.GetTempPath());
        using var provider = services.BuildServiceProvider();

        provider.GetServices<RegisteredToolInvoker>().ShouldHaveSingleItem().Descriptor.Id.ShouldBe(ReadFileTool.Id);
    }

    [Fact]
    public void AddReadTool_WhenAnotherToolIsRegistered_PreservesBothRegistrations()
    {
        var services = new ServiceCollection();
        _ = TestFactory.AddToolDependencies(services);
        _ = services.AddToolInvoker<StubInvoker>(StubTool.Descriptor);

        _ = services.AddReadTool(static options => options.HostRootPath = Path.GetTempPath());
        using var provider = services.BuildServiceProvider();

        provider.GetServices<RegisteredToolInvoker>()
            .Select(static registration => registration.Descriptor.Id)
            .ShouldBe([StubTool.Id, ReadFileTool.Id], ignoreOrder: true);
    }

    [Fact]
    public void AddReadTool_WhenComposedWithAgentTools_RegistersCatalogCoordinator()
    {
        var services = new ServiceCollection();
        _ = TestFactory.AddToolDependencies(services);
        _ = services.AddAgentTools();
        _ = services.AddReadTool(static options => options.HostRootPath = Path.GetTempPath());
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IToolCatalog>().ShouldBeOfType<ToolCatalogCoordinator>();
    }

    private sealed class StubInvoker: IToolInvoker
    {
        public ValueTask<ToolInvocationResult> InvokeAsync(ToolInvocationContext context, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static class StubTool
    {
        public static readonly ToolId Id = new("stub");

        public static ToolDescriptor Descriptor { get; } = new(
            Id,
            new ToolVersion("1"),
            "stub",
            "stub",
            new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), JsonDocument.Parse("{}").RootElement),
            null,
            new ToolEffects(ToolEffect.ReadOnly, null, null),
            new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null),
            new ToolSourceId("test"),
            ExtensionData.Empty);
    }
}
