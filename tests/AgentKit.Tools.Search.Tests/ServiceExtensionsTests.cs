// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Search.Tests;

using AgentKit.TestSupport;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddSearchTool_WhenCalledTwice_RegistersOneTool()
    {
        var services = CreateServices();
        _ = services.AddSearchTool();
        _ = services.AddSearchTool();
        using var provider = services.BuildServiceProvider();
        _ = provider.GetServices<ITool>().ShouldHaveSingleItem().ShouldBeOfType<SearchTool>();
    }

    [Fact]
    public void AddSearchTool_WhenOptionsInvalid_FailsValidation()
    {
        var services = CreateServices();
        _ = services.AddSearchTool(static options => options.DefaultMaximumBytes = options.MaximumBytes + 1);
        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SearchToolOptions>>().Value);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileContentSearcher, FakeFileContentSearcher>();
        _ = services.AddSingleton<ISecurityAuthority, RecordingSecurityAuthority>();
        _ = services.AddSingleton<ISecurityAuthoritySelector>(sp => new FixedSecurityAuthoritySelector(sp.GetRequiredService<ISecurityAuthority>()));
        _ = services.AddSingleton<IIdentifierGenerator<SecurityRequestId>, StubSecurityRequestIdGenerator>();
        _ = services.AddSingleton<TimeProvider, FixedTimeProvider>();
        return services;
    }
}
