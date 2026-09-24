// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddWriteTool_WhenServicesNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(() => services.AddWriteTool());

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddWriteTool_WhenCalled_RegistersWriteFileTool()
    {
        var services = new ServiceCollection();
        _ = TestFactory.AddToolDependencies(services);

        _ = services.AddWriteTool(static options => options.HostRootPath = Path.GetTempPath());
        using var provider = services.BuildServiceProvider();

        provider.GetServices<RegisteredToolInvoker>().ShouldHaveSingleItem().Descriptor.Id.ShouldBe(WriteFileTool.Id);
    }

    [Fact]
    public void AddWriteTool_WhenHostRootMissing_FailsValidation()
    {
        var services = new ServiceCollection();
        _ = TestFactory.AddToolDependencies(services);
        _ = services.AddWriteTool();
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<WriteFileToolOptions>>().Value);
    }

    [Fact]
    public void AddWriteTool_WhenCalledTwice_RegistersWriteFileToolOnce()
    {
        var services = new ServiceCollection();
        _ = TestFactory.AddToolDependencies(services);

        _ = services.AddWriteTool(static options => options.HostRootPath = Path.GetTempPath());
        _ = services.AddWriteTool(static options => options.HostRootPath = Path.GetTempPath());
        using var provider = services.BuildServiceProvider();

        provider.GetServices<RegisteredToolInvoker>().ShouldHaveSingleItem().Descriptor.Id.ShouldBe(WriteFileTool.Id);
    }

    [Fact]
    public void AddWriteTool_WhenAnotherToolIsRegistered_PreservesBothRegistrations()
    {
        var services = new ServiceCollection();
        _ = TestFactory.AddToolDependencies(services);
        _ = services.AddToolInvoker<StubInvoker>(StubTool.Descriptor);

        _ = services.AddWriteTool(static options => options.HostRootPath = Path.GetTempPath());
        using var provider = services.BuildServiceProvider();

        provider.GetServices<RegisteredToolInvoker>()
            .Select(static registration => registration.Descriptor.Id)
            .ShouldBe([StubTool.Id, WriteFileTool.Id], ignoreOrder: true);
    }

    [Fact]
    public void AddWriteTool_WhenComposedWithAgentTools_RegistersCatalogCoordinator()
    {
        var services = new ServiceCollection();
        _ = TestFactory.AddToolDependencies(services);
        _ = services.AddAgentTools();
        _ = services.AddWriteTool(static options => options.HostRootPath = Path.GetTempPath());
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
