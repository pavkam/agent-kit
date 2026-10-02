// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Tests;

/// <summary>Verifies <see cref="OperatingSystemExecutableResolver"/> maps start requests to resolved facts.</summary>
public sealed class OperatingSystemExecutableResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenExecutableIsAllowed_ReturnsResolvedStart()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        using var root = new TempProcessRoot(["/bin/echo"]);
        var resolver = CreateResolver(root);
        var request = StartRequest("/bin/echo", ["hello"]);

        var result = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        var resolved = result.ShouldBeOfType<ExecutableResolved>().Resolved;
        var binDirectory = Directory.ResolveLinkTarget("/bin", returnFinalTarget: true)?.FullName ?? "/bin";
        resolved.Executable.AbsolutePath.ShouldBe(Path.Combine(binDirectory, "echo"));
        NormalizeTempPath(resolved.WorkingDirectory)
            .ShouldBe(NormalizeTempPath(Path.Combine(root.Path, "run")));
    }

    [Fact]
    public async Task ResolveAsync_WhenExecutableIsNotAllowed_ReturnsFailed()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        using var root = new TempProcessRoot(["/bin/echo"]);
        var resolver = CreateResolver(root);
        var request = StartRequest("/bin/sh", []);

        var result = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ExecutableResolutionFailed>();
    }

    private static string NormalizeTempPath(string path) =>
        path.Replace("/private", string.Empty, StringComparison.Ordinal);

    private static OperatingSystemExecutableResolver CreateResolver(TempProcessRoot root)
    {
        var snapshot = new AgentProcessOptionsSnapshot(
            new ProcessExecutorKey("test"),
            new ProcessExecutorVersion(1),
            root.Options,
            ProcessWorkspaceAccess.ReadOnly,
            ProcessChildPolicy.AllowSandboxed);
        var intentResolver = new OperatingSystemProcessIntentResolver(Options.Create(root.Options));
        return new OperatingSystemExecutableResolver(snapshot, intentResolver);
    }

    private static ProcessStartRequest StartRequest(string executable, string[] arguments)
    {
        var rootId = new FileRootId("workspace");
        return new ProcessStartRequest(
            new ProcessOperationId(Guid.NewGuid()),
            new OperationId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            null,
            new ProcessExecutableReference(executable),
            [.. arguments.Select(static argument => new ProcessArgument(argument))],
            new FileTarget(rootId, new NormalizedRelativePath("run")),
            new EnvironmentProjection([]),
            null,
            PlatformProcessSandboxProvider.WorkspaceNoNetworkProfile,
            new ProcessResourceLimits(TimeSpan.FromSeconds(5), 4096, TimeSpan.FromMilliseconds(250)),
            ProcessEffectClass.ReadOnlyObservation);
    }

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
        }

        internal string Path { get; }

        internal OperatingSystemProcessOptions Options { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
