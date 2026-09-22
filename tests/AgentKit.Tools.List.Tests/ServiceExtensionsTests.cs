// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List.Tests;

using AgentKit.TestSupport;

using AgentKit.Tools;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddListTool_WhenCalledTwice_RegistersOneInvokerAndOneLegacyTool()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ILegacyDirectoryReader, FakeDirectoryReader>();
        _ = services.AddSingleton<ISecurityAuthority, RecordingSecurityAuthority>();
        _ = services.AddSingleton<ISecurityAuthoritySelector>(static provider =>
            new FixedSecurityAuthoritySelector(provider.GetRequiredService<ISecurityAuthority>()));
        _ = services.AddSingleton<IIdentifierGenerator<SecurityRequestId>, StubSecurityRequestIdGenerator>();
        _ = services.AddSingleton<TimeProvider, FixedTimeProvider>();

        _ = services.AddListTool();
        _ = services.AddListTool();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetServices<ITool>().ShouldHaveSingleItem().ShouldBeOfType<ListDirectoryTool>();
        var identity = new ToolIdentity(ListDirectoryTool.Id, ListDirectoryTool.Descriptor.Version);
        _ = provider.GetKeyedService<IToolInvoker>(identity).ShouldBeOfType<ListDirectoryTool>();
        services.Count(static descriptor =>
                descriptor.IsKeyedService
                && descriptor.ServiceType == typeof(IToolProvider)
                && descriptor.ServiceKey is ToolSourceId sourceId
                && sourceId == ApplicationToolSources.Default)
            .ShouldBe(1);
    }

    [Fact]
    public void AddListTool_WhenConfigureProvided_AppliesConfiguredBounds()
    {
        var services = new ServiceCollection();
        _ = services.AddListTool(static options => options.DefaultPageEntries = 5);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<ListDirectoryToolOptions>>().Value.DefaultPageEntries.ShouldBe(5);
    }

    [Fact]
    public void AddListTool_WhenBoundsInvalid_FailsOptionsValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddListTool(static options => options.DefaultPageEntries = options.MaximumPageEntries + 1);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ListDirectoryToolOptions>>().Value);
    }
}
