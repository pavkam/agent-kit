// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    private static readonly ProcessOperationId _operationId = new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    [Fact]
    [Obsolete("Legacy host surface.")]
    public async Task AddScriptedProcesses_WhenIntentGeneratorIsHostSupplied_UsesTheReplacement()
    {
        var store = new TestGrantStore();
        var expectedId = new SecurityEnforcementIntentId(Guid.Parse("81000000-0000-0000-0000-000000000008"));
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(new SequenceSecurityEnforcementIntentIdGenerator(expectedId.Value));
        _ = services.AddScriptedProcesses(options =>
        {
            options.Executables.Add(new ScriptedExecutable("tool", "/scripted/bin/tool", new ContentHash("sha256:tool")));
            options.Scenarios.Add(new ScriptedProcessScenario(_operationId, Success("done"), TimeSpan.Zero));
        });
        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IProcessIntentResolver>();
        var intent = (await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        _ = await provider.GetRequiredService<IProcessRunner>().RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(expectedId);
    }

    private static ProcessResolveRequest Request() => new(_operationId, "tool", ["literal *", "$(never)"], new FileSystemPath("src"), [], [], new SandboxProfileId("scripted"), ProcessWorkspaceAccess.ReadOnly, ProcessSideEffectClass.ReadOnly, ProcessChildPolicy.Deny, new ProcessResourceLimits(TimeSpan.FromSeconds(1), 100, TimeSpan.Zero));
    private static ProcessRunResult Success(string text) => new(ProcessRunStatus.Exited, 0, [.. Encoding.UTF8.GetBytes(text)], [], Encoding.UTF8.GetByteCount(text), 0, false, false, ProcessSideEffectCertainty.Completed, null);
}
