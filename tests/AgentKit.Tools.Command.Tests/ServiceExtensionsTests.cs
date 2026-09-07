// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Command.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddCommandTool_WhenCalledTwice_RegistersOneTool()
    {
        var services = Dependencies();
        _ = services.AddCommandTool();
        _ = services.AddCommandTool();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetServices<ITool>().ShouldHaveSingleItem().ShouldBeOfType<CommandTool>();
    }

    [Fact]
    public void AddCommandTool_WhenOptionsInvalid_FailsValidation()
    {
        var services = Dependencies();
        _ = services.AddCommandTool(static options => options.MaximumCommandBytes = 0);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CommandToolOptions>>().Value);
    }

    private static ServiceCollection Dependencies()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IProcessIntentResolver, RecordingProcessResolver>();
        _ = services.AddSingleton<IProcessRunner, RecordingProcessRunner>();
        _ = services.AddSingleton<ISecurityAuthority, RecordingSecurityAuthority>();
        _ = services.AddSingleton<IIdentifierGenerator<SecurityRequestId>, FixedSecurityRequestIdGenerator>();
        _ = services.AddSingleton<IIdentifierGenerator<ProcessOperationId>, FixedProcessOperationIdGenerator>();
        _ = services.AddSingleton<TimeProvider, FixedTimeProvider>();
        return services;
    }
}
