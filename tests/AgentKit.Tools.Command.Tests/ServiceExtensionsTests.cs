// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Command.Tests;

using AgentKit.TestSupport;

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

    [Fact]
    public void AddCommandTool_WhenDefaultOptionsWithValidEnvironmentVariable_PassesValidation()
    {
        var services = Dependencies();
        _ = services.AddCommandTool(static options => options.EnvironmentVariables.Add("PATH", "/usr/bin"));
        using var provider = services.BuildServiceProvider();

        _ = Should.NotThrow(() => provider.GetRequiredService<IOptions<CommandToolOptions>>().Value);
    }

    [Theory]
    [InlineData("", "value")]
    [InlineData("KEY=BAD", "value")]
    [InlineData("KEY\0BAD", "value")]
    public void AddCommandTool_WhenEnvironmentVariableKeyIsInvalid_FailsValidation(string key, string value)
    {
        var services = Dependencies();
        _ = services.AddCommandTool(options => options.EnvironmentVariables.Add(key, value));
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CommandToolOptions>>().Value);
    }

    [Fact]
    public void AddCommandTool_WhenEnvironmentVariableValueContainsNul_FailsValidation()
    {
        var services = Dependencies();
        _ = services.AddCommandTool(static options => options.EnvironmentVariables.Add("KEY", "bad\0value"));
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
        _ = services.AddSingleton<ISecurityAuthoritySelector>(sp => new FixedSecurityAuthoritySelector(sp.GetRequiredService<ISecurityAuthority>()));
        _ = services.AddSingleton<IIdentifierGenerator<SecurityRequestId>, FixedSecurityRequestIdGenerator>();
        _ = services.AddSingleton<IIdentifierGenerator<ProcessOperationId>, FixedProcessOperationIdGenerator>();
        _ = services.AddSingleton<TimeProvider, FixedTimeProvider>();
        return services;
    }
}
