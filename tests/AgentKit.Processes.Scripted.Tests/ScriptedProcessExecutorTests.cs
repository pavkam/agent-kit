// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted.Tests;

/// <summary>Verifies <see cref="ScriptedProcessExecutor"/> behavior and contracts.</summary>
public sealed class ScriptedProcessExecutorTests
{
    private static readonly ProcessOperationId _operationId = new(Guid.Parse("50000000-0000-0000-0000-000000000005"));

    [Fact]
    public void Equality_WhenOperationIdExitAndOutputMatch_TreatsScenariosAsEqual()
    {
        var exit = new ProcessExited(0, SideEffectCertainty.DefinitelyPerformed);
        var stdout = ImmutableArray.Create((byte) 1, (byte) 2);
        var first = new ScriptedProcessScenario(_operationId, exit, stdout, [], TimeSpan.Zero);
        var second = new ScriptedProcessScenario(_operationId, exit, stdout, [], TimeSpan.Zero);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        (first with { }).ShouldBe(first);
    }

    [Fact]
    public async Task StartAsync_WhenScenarioConfigured_ConsumesExactGrantAndReturnsDeclaredOutput()
    {
        var resolved = await ResolveAsync();
        var expected = Success("done");
        var store = new TestGrantStore();
        var executor = Executor(store, expected, TimeSpan.Zero, TimeProvider.System);
        var start = await executor.StartAsync(resolved, TestGrantStore.Grant(), TestContext.Current.CancellationToken);
        var handle = start.ShouldBeOfType<ProcessHandleStarted>().Handle;
        await using (handle)
        {
            var output = new List<byte>();
            await foreach (var evt in handle.ReadOutputAsync(TestContext.Current.CancellationToken))
            {
                if (evt is ProcessStandardOutputBytes bytes)
                {
                    output.AddRange(bytes.Bytes.ToArray());
                }
            }

            output.ToArray().ShouldBe("done"u8.ToArray());
            _ = (await handle.Completion).ShouldBeOfType<ProcessExited>();
        }

        var enforcement = store.Enforcements.ShouldHaveSingleItem();
        enforcement.Resources.ShouldBe(ProcessSecurityBinding.Resources(await IntentAsync(resolved)));
        enforcement.InputFingerprint.ShouldBe(ProcessSecurityBinding.Fingerprint(await IntentAsync(resolved)));
    }

    [Fact]
    public async Task StartAsync_WhenNoScenarioIsConfigured_ReturnsFailedWithoutConsumingTheGrant()
    {
        var resolved = await ResolveAsync();
        var store = new TestGrantStore();
        var snapshot = Snapshot([]);
        var executor = new ScriptedProcessExecutor(
            snapshot,
            new ScriptedExecutableResolver(snapshot, Resolver()),
            store,
            TimeProvider.System,
            new GuidSecurityEnforcementIntentIdGenerator());
        var start = await executor.StartAsync(resolved, TestGrantStore.Grant(), TestContext.Current.CancellationToken);
        _ = start.ShouldBeOfType<ProcessStartFailed>();
        store.Enforcements.ShouldBeEmpty();
    }

    [Fact]
    public async Task StartAsync_WhenGrantDenied_ReturnsDeniedInsteadOfScenario()
    {
        var resolved = await ResolveAsync();
        var store = new TestGrantStore { Status = GrantConsumptionStatus.Unknown };
        var executor = Executor(store, Success("never"), TimeSpan.Zero, TimeProvider.System);
        var start = await executor.StartAsync(resolved, TestGrantStore.Grant(), TestContext.Current.CancellationToken);
        _ = start.ShouldBeOfType<ProcessStartDenied>();
    }

    [Fact]
    public async Task StartAsync_WhenCallerAlreadyCancelled_ThrowsWithoutConsumingTheGrant()
    {
        var resolved = await ResolveAsync();
        var store = new TestGrantStore();
        var executor = Executor(store, Success("never"), TimeSpan.Zero, TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var action = async () => await executor.StartAsync(resolved, TestGrantStore.Grant(), cancellation.Token);
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        store.Enforcements.ShouldBeEmpty();
    }

    private static async Task<ResolvedProcessStart> ResolveAsync()
    {
        var resolver = new ScriptedExecutableResolver(Snapshot([Success("placeholder")]), Resolver());
        var request = StartRequest();
        var resolution = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);
        return resolution.ShouldBeOfType<ExecutableResolved>().Resolved;
    }

    private static Task<ResolvedProcessIntent> IntentAsync(ResolvedProcessStart resolved) =>
                Task.FromResult(ProcessStartBinding.ToResolvedProcessIntent(resolved));

    private static ScriptedProcessExecutor Executor(
        ISecurityGrantStore store,
        ScriptedProcessScenario scenario,
        TimeSpan delay,
        TimeProvider timeProvider,
        IIdentifierGenerator<SecurityEnforcementIntentId>? intentIds = null)
    {
        var snapshot = Snapshot([new ScriptedProcessScenario(
            scenario.OperationId,
            scenario.Exit,
            scenario.StandardOutput,
            scenario.StandardError,
            delay,
            scenario.StandardOutputTruncated,
            scenario.StandardErrorTruncated,
            scenario.TotalStandardOutputBytes,
            scenario.TotalStandardErrorBytes)]);
        return new ScriptedProcessExecutor(
            snapshot,
            new ScriptedExecutableResolver(snapshot, Resolver()),
            store,
            timeProvider,
            intentIds ?? new GuidSecurityEnforcementIntentIdGenerator());
    }

    private static ScriptedProcessOptionsSnapshot Snapshot(IEnumerable<ScriptedProcessScenario> scenarios)
    {
        var options = new ScriptedProcessOptions();
        options.Executables.Add(new ScriptedExecutable("tool", "/scripted/bin/tool", new ContentHash("sha256:tool")));
        foreach (var scenario in scenarios)
        {
            options.Scenarios.Add(scenario);
        }

        return new ScriptedProcessOptionsSnapshot(
            new ProcessExecutorKey("test"),
            new ProcessExecutorVersion(1),
            options,
            options.Scenarios.ToImmutableDictionary(static item => item.OperationId));
    }

    private static ScriptedProcessIntentResolver Resolver()
    {
        var options = new ScriptedProcessOptions();
        options.Executables.Add(new ScriptedExecutable("tool", "/scripted/bin/tool", new ContentHash("sha256:tool")));
        return new ScriptedProcessIntentResolver(Options.Create(options));
    }

    private static ProcessStartRequest StartRequest() => new(
        _operationId,
        new OperationId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
        new AgentId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
        null,
        new ProcessExecutableReference("tool"),
        [new ProcessArgument("literal *"), new ProcessArgument("$(never)")],
        new FileTarget(new FileRootId("workspace"), new NormalizedRelativePath("src")),
        new EnvironmentProjection([]),
        null,
        new SandboxProfileId("scripted"),
        new ProcessResourceLimits(TimeSpan.FromSeconds(1), 100, TimeSpan.Zero),
        ProcessEffectClass.ReadOnlyObservation);

    private static ScriptedProcessScenario Success(string text) => new(
        _operationId,
        new ProcessExited(0, SideEffectCertainty.DefinitelyPerformed),
        [.. Encoding.UTF8.GetBytes(text)],
        [],
        TimeSpan.Zero);
}
