// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddOperatingSystemProcesses_WhenRegistered_ProvidesResolverSandboxAndOperationIds()
    {
        var services = new ServiceCollection();
        var intentIds = new SequenceSecurityEnforcementIntentIdGenerator(
            Guid.Parse("82000000-0000-0000-0000-000000000008"));
        _ = services.AddSingleton<ISecurityGrantStore>(new TestGrantStore());
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(intentIds);
        _ = services.AddOperatingSystemProcesses(
            Path.GetTempPath(),
            static options => options.AllowedExecutablePaths.Add("/bin/sh"));
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IProcessIntentResolver>().ShouldBeOfType<OperatingSystemProcessIntentResolver>();
        _ = provider.GetServices<IProcessSandboxProvider>().ShouldHaveSingleItem()
            .ShouldBeOfType<PlatformProcessSandboxProvider>();
        provider.GetRequiredService<IIdentifierGenerator<ProcessOperationId>>().Create().Value.ShouldNotBe(Guid.Empty);
        provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>().ShouldBeSameAs(intentIds);
        _ = provider.GetRequiredService<IProcessRunner>().ShouldBeOfType<OperatingSystemProcessRunner>();
    }

    [Fact]
    public void AddOperatingSystemProcesses_WhenForcedTerminationWaitInvalid_FailsOptionsValidation()
    {
        var services = new ServiceCollection();
        _ = services.AddOperatingSystemProcesses(Path.GetTempPath(), static options =>
        {
            options.AllowedExecutablePaths.Add("/bin/sh");
            options.ForcedTerminationWait = TimeSpan.Zero;
        });
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<OperatingSystemProcessOptions>>().Value);
    }
}
