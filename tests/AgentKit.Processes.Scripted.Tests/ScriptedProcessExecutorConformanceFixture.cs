// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted.Tests;

using AgentKit.Conformance;
using AgentKit.Permissions.InMemory;

/// <summary>Scripted adapter fixture for shared process executor conformance scenarios.</summary>
public sealed class ScriptedProcessExecutorConformanceFixture: IProcessExecutorConformanceFixture, IAsyncDisposable
{
    private static readonly ProcessOperationId _operationId = new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    private readonly ServiceProvider _provider;
    private readonly InMemorySecurityGrantStore _grantStore;

    internal ScriptedProcessExecutorConformanceFixture()
    {
        var services = new ServiceCollection();
        _grantStore = new InMemorySecurityGrantStore(TimeProvider.System);
        _ = services.AddSingleton<ISecurityGrantStore>(_grantStore);
        _ = services.AddAgentScriptedProcesses(new ProcessExecutorKey("conformance"), static options =>
        {
            options.Executables.Add(new ScriptedExecutable("tool", "/scripted/bin/tool", new ContentHash("sha256:tool")));
            options.Scenarios.Add(new ScriptedProcessScenario(
                _operationId,
                new ProcessExited(0, SideEffectCertainty.DefinitelyPerformed),
                [.. "done"u8],
                [],
                TimeSpan.Zero));
        });
        _provider = services.BuildServiceProvider();
    }

    /// <inheritdoc/>
    public IExecutableResolver Resolver => _provider.GetRequiredKeyedService<IExecutableResolver>("conformance");

    /// <inheritdoc/>
    public IProcessExecutor Executor => _provider.GetRequiredKeyedService<IProcessExecutor>("conformance");

    /// <inheritdoc/>
    public ProcessStartRequest CreateStartRequest() => new(
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

    /// <inheritdoc/>
    public async ValueTask<SecurityGrant> CreateAuthorizedGrantAsync(ProcessStartRequest request)
    {
        var resolution = await Resolver.ResolveAsync(request).ConfigureAwait(false);
        var resolved = resolution.ShouldBeOfType<ExecutableResolved>().Resolved;
        var intent = ProcessStartBinding.ToResolvedProcessIntent(resolved);
        var grant = CapturedGrantFactory.Create(
            Executor.SecurityAudience,
            ProcessSecurityBinding.Resources(intent),
            ProcessSecurityBinding.Fingerprint(intent));
        await _grantStore.RegisterAsync(grant).ConfigureAwait(false);
        return grant;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _provider.Dispose();
        return ValueTask.CompletedTask;
    }
}
