// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentProcesses_WhenRegistered_ProvidesKeyedExecutorResolverAndOperationIds()
    {
        var services = new ServiceCollection();
        var intentIds = new SequenceSecurityEnforcementIntentIdGenerator(
            Guid.Parse("82000000-0000-0000-0000-000000000008"));
        _ = services.AddSingleton<ISecurityGrantStore>(new TestGrantStore());
        _ = services.AddSingleton<ISecurityAuditDispatcher, NoOpSecurityAuditDispatcher>();
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(intentIds);
        _ = services.AddAgentProcesses(new ProcessExecutorKey("default"), static options =>
        {
            options.OperatingSystem.RootDirectory = Path.GetTempPath();
            options.OperatingSystem.AllowedExecutablePaths.Add("/bin/sh");
        });
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<IExecutableResolver>("default");
        _ = provider.GetRequiredKeyedService<IProcessExecutor>("default");
        _ = provider.GetServices<IProcessSandboxProvider>().ShouldHaveSingleItem()
            .ShouldBeOfType<PlatformProcessSandboxProvider>();
        provider.GetRequiredService<IIdentifierGenerator<ProcessOperationId>>().Create().Value.ShouldNotBe(Guid.Empty);
        provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>().ShouldBeSameAs(intentIds);
        _ = provider.GetRequiredService<IProcessExecutorSelector>();
    }

    private sealed class NoOpSecurityAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }
}
