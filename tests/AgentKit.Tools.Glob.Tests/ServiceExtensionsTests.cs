// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Glob.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddGlobTool_WhenCalledTwice_RegistersOneTool()
    {
        var services = CreateServices();

        _ = services.AddGlobTool();
        _ = services.AddGlobTool();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetServices<ITool>().ShouldHaveSingleItem().ShouldBeOfType<GlobTool>();
    }

    [Fact]
    public void AddGlobTool_WhenOptionsInvalid_FailsValidation()
    {
        var services = CreateServices();
        _ = services.AddGlobTool(static options => options.DefaultMaximumResults = options.MaximumResults + 1);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GlobToolOptions>>().Value);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IFileGlobber, FakeFileGlobber>();
        _ = services.AddSingleton<ISecurityAuthority, RecordingSecurityAuthority>();
        _ = services.AddSingleton<IIdentifierGenerator<SecurityRequestId>, StubSecurityRequestIdGenerator>();
        _ = services.AddSingleton<TimeProvider, FixedTimeProvider>();
        return services;
    }
}
