// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted.Tests;

/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    private static readonly ProcessOperationId _operationId = new(Guid.Parse("50000000-0000-0000-0000-000000000005"));

    [Fact]
    public async Task AddAgentScriptedProcesses_WhenIntentGeneratorIsHostSupplied_UsesTheReplacement()
    {
        var store = new TestGrantStore();
        var expectedId = new SecurityEnforcementIntentId(Guid.Parse("81000000-0000-0000-0000-000000000008"));
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(new SequenceSecurityEnforcementIntentIdGenerator(expectedId.Value));
        _ = services.AddAgentScriptedProcesses(new ProcessExecutorKey("scripted"), options =>
        {
            options.Executables.Add(new ScriptedExecutable("tool", "/scripted/bin/tool", new ContentHash("sha256:tool")));
            options.Scenarios.Add(new ScriptedProcessScenario(
                _operationId,
                new ProcessExited(0, SideEffectCertainty.DefinitelyPerformed),
                [.. "done"u8],
                [],
                TimeSpan.Zero));
        });
        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredKeyedService<IExecutableResolver>("scripted");
        var request = StartRequest();
        var resolved = (await resolver.ResolveAsync(request, TestContext.Current.CancellationToken))
            .ShouldBeOfType<ExecutableResolved>().Resolved;
        var executor = provider.GetRequiredKeyedService<IProcessExecutor>("scripted");
        _ = await executor.StartAsync(resolved, TestGrantStore.Grant(), TestContext.Current.CancellationToken);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(expectedId);
    }

    private static ProcessStartRequest StartRequest() => new(
        _operationId,
        new OperationId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
        new AgentId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
        null,
        new ProcessExecutableReference("tool"),
        [new ProcessArgument("literal")],
        new FileTarget(new FileRootId("workspace"), new NormalizedRelativePath("src")),
        new EnvironmentProjection([]),
        null,
        new SandboxProfileId("scripted"),
        new ProcessResourceLimits(TimeSpan.FromSeconds(1), 100, TimeSpan.Zero),
        ProcessEffectClass.ReadOnlyObservation);
}
