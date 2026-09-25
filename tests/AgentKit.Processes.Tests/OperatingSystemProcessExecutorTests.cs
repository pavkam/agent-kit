// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Tests;

/// <summary>Verifies <see cref="OperatingSystemProcessExecutor"/> fail-closed start behavior.</summary>
public sealed class OperatingSystemProcessExecutorTests
{
    [Fact]
    public async Task StartAsync_WhenSandboxProfileIsMissing_ReturnsSandboxUnavailable()
    {
        using var root = new TempProcessRoot(["/bin/echo"]);
        var resolver = CreateResolver(root);
        var executor = CreateExecutor(root, resolver, sandboxes: []);
        var startRequest = await resolver.ResolveAsync(CreateStartRequest("/bin/echo", []), TestContext.Current.CancellationToken);
        var resolved = startRequest.ShouldBeOfType<ExecutableResolved>().Resolved;

        var result = await executor.StartAsync(resolved, TestGrantStore.Grant(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ProcessStartSandboxUnavailable>();
    }

    private static OperatingSystemExecutableResolver CreateResolver(TempProcessRoot root)
    {
        var snapshot = root.Snapshot;
        var intentResolver = new OperatingSystemProcessIntentResolver(Options.Create(root.Options));
        return new OperatingSystemExecutableResolver(snapshot, intentResolver);
    }

    private static OperatingSystemProcessExecutor CreateExecutor(
        TempProcessRoot root,
        IExecutableResolver resolver,
        IEnumerable<IProcessSandboxProvider> sandboxes)
    {
        return new OperatingSystemProcessExecutor(
            root.Snapshot,
            resolver,
            new DefaultProcessSandboxSelector(sandboxes),
            new TestGrantStore(),
            new NoOpSecurityAuditDispatcher(),
            new GuidSecurityAuditRecordIdGenerator(),
            TimeProvider.System,
            new GuidSecurityEnforcementIntentIdGenerator());
    }

    private static ProcessStartRequest CreateStartRequest(string executable, string[] arguments) =>
        new(
            new ProcessOperationId(Guid.NewGuid()),
            new OperationId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            null,
            new ProcessExecutableReference(executable),
            [.. arguments.Select(static argument => new ProcessArgument(argument))],
            new FileTarget(new FileRootId("workspace"), new NormalizedRelativePath("run")),
            new EnvironmentProjection([]),
            null,
            PlatformProcessSandboxProvider.WorkspaceNoNetworkProfile,
            new ProcessResourceLimits(TimeSpan.FromSeconds(5), 4096, TimeSpan.FromMilliseconds(250)),
            ProcessEffectClass.ReadOnlyObservation);

    private sealed class TempProcessRoot: IDisposable
    {
        internal TempProcessRoot(IEnumerable<string> allowedExecutables)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"agentkit-process-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(Path);
            _ = Directory.CreateDirectory(System.IO.Path.Combine(Path, "run"));
            Options = new OperatingSystemProcessOptions { RootDirectory = Path };
            foreach (var executable in allowedExecutables)
            {
                Options.AllowedExecutablePaths.Add(executable);
            }

            Snapshot = new AgentProcessOptionsSnapshot(
                new ProcessExecutorKey("test"),
                new ProcessExecutorVersion(1),
                Options,
                ProcessWorkspaceAccess.ReadOnly,
                ProcessChildPolicy.AllowSandboxed);
        }

        internal string Path { get; }

        internal OperatingSystemProcessOptions Options { get; }

        internal AgentProcessOptionsSnapshot Snapshot { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class NoOpSecurityAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }
}
