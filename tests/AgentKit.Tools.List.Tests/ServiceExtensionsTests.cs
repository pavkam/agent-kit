// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List.Tests;

using AgentKit.TestSupport;

public sealed class ServiceExtensionsTests
{
    [Fact]
    [Obsolete("Legacy host surface.")]

    public void AddListTool_WhenCalledTwice_RegistersOneTool()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ILegacyDirectoryReader, FakeDirectoryReader>();
        _ = services.AddSingleton<ISecurityAuthority, RecordingSecurityAuthority>();
        _ = services.AddSingleton<ISecurityAuthoritySelector>(sp => new FixedSecurityAuthoritySelector(sp.GetRequiredService<ISecurityAuthority>()));
        _ = services.AddSingleton<IIdentifierGenerator<SecurityRequestId>, StubSecurityRequestIdGenerator>();
        _ = services.AddSingleton<TimeProvider, FixedTimeProvider>();

        _ = services.AddListTool();
        _ = services.AddListTool();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetServices<ITool>().ShouldHaveSingleItem().ShouldBeOfType<ListDirectoryTool>();
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
