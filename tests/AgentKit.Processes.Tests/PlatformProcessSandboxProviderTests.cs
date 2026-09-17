// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Tests;

/// <summary>Verifies PlatformProcessSandboxProvider behavior and contracts.</summary>
public sealed class PlatformProcessSandboxProviderTests
{
    private const string _sandboxExecPath = "/usr/bin/sandbox-exec";
    private const string _bubblewrapPath = "/usr/bin/bwrap";

    [Fact]
    public async Task PrepareAsync_WhenWorkspaceAccessIsNone_ReturnsUnsupportedIntentRegardlessOfPlatform()
    {
        var provider = new PlatformProcessSandboxProvider(new FakeProcessSandboxPlatformProbe(isMacOs: true));

        var result = await provider.PrepareAsync(
            Intent(workspaceAccess: ProcessWorkspaceAccess.None), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessSandboxStatus.UnsupportedIntent);
        result.Launch.ShouldBeNull();
        result.SafeMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PrepareAsync_WhenPlatformIsNeitherMacOsNorLinux_ReturnsUnavailable()
    {
        var provider = new PlatformProcessSandboxProvider(new FakeProcessSandboxPlatformProbe(isMacOs: false, isLinux: false));

        var result = await provider.PrepareAsync(Intent(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessSandboxStatus.Unavailable);
        result.Launch.ShouldBeNull();
        result.SafeMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PrepareAsync_WhenMacOsSandboxExecMissing_ReturnsUnavailable()
    {
        var provider = new PlatformProcessSandboxProvider(
            new FakeProcessSandboxPlatformProbe(isMacOs: true, fileExists: static _ => false));

        var result = await provider.PrepareAsync(Intent(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessSandboxStatus.Unavailable);
        result.Launch.ShouldBeNull();
        result.SafeMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PrepareAsync_WhenMacOsChildPolicyAllowSandboxed_AllowsAnyProcessExec()
    {
        var provider = MacOsProvider();

        var result = await provider.PrepareAsync(
            Intent(childPolicy: ProcessChildPolicy.AllowSandboxed), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessSandboxStatus.Ready);
        var launch = result.Launch.ShouldNotBeNull();
        launch.ExecutablePath.ShouldBe(_sandboxExecPath);
        string.Join('\n', launch.Arguments).ShouldContain("(allow process*)");
    }

    [Fact]
    public async Task PrepareAsync_WhenMacOsChildPolicyDeny_RestrictsProcessExecToTheLiteralExecutable()
    {
        var provider = MacOsProvider();

        var result = await provider.PrepareAsync(
            Intent(childPolicy: ProcessChildPolicy.Deny), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessSandboxStatus.Ready);
        var profile = string.Join('\n', result.Launch.ShouldNotBeNull().Arguments);
        profile.ShouldNotContain("(allow process*)");
        profile.ShouldContain("(allow process-exec (literal \"/bin/sh\"))");
    }

    [Fact]
    public async Task PrepareAsync_WhenMacOsWorkspaceAccessReadOnly_OmitsFileWriteAllowRule()
    {
        var provider = MacOsProvider();

        var result = await provider.PrepareAsync(
            Intent(workspaceAccess: ProcessWorkspaceAccess.ReadOnly), TestContext.Current.CancellationToken);

        string.Join('\n', result.Launch.ShouldNotBeNull().Arguments).ShouldNotContain("(allow file-write*");
    }

    [Fact]
    public async Task PrepareAsync_WhenMacOsWorkspaceAccessReadWrite_AddsFileWriteAllowRule()
    {
        var provider = MacOsProvider();

        var result = await provider.PrepareAsync(
            Intent(workspaceAccess: ProcessWorkspaceAccess.ReadWrite), TestContext.Current.CancellationToken);

        string.Join('\n', result.Launch.ShouldNotBeNull().Arguments).ShouldContain("(allow file-write*");
    }

    [Fact]
    public async Task PrepareAsync_WhenMacOsReadOnlyRootCaptured_ProjectsOnlyThatExternalRootReadOnly()
    {
        var provider = MacOsProvider();
        var root = "/opt/agentkit-test-toolchain";

        var result = await provider.PrepareAsync(
            Intent(roots: [new ProcessReadOnlyRoot("test-toolchain", root)]), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessSandboxStatus.Ready);
        string.Join('\n', result.Launch.ShouldNotBeNull().Arguments).ShouldContain(root);
        var withoutRoot = await provider.PrepareAsync(Intent(), TestContext.Current.CancellationToken);
        string.Join('\n', withoutRoot.Launch.ShouldNotBeNull().Arguments).ShouldNotContain(root);
    }

    [Fact]
    public async Task PrepareAsync_WhenLinuxBubblewrapMissing_ReturnsUnavailable()
    {
        var provider = new PlatformProcessSandboxProvider(
            new FakeProcessSandboxPlatformProbe(isLinux: true, fileExists: static _ => false));

        var result = await provider.PrepareAsync(Intent(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessSandboxStatus.Unavailable);
        result.Launch.ShouldBeNull();
        result.SafeMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PrepareAsync_WhenLinuxChildPolicyDeny_ReturnsUnsupportedIntent()
    {
        var provider = LinuxProvider();

        var result = await provider.PrepareAsync(
            Intent(childPolicy: ProcessChildPolicy.Deny), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessSandboxStatus.UnsupportedIntent);
        result.Launch.ShouldBeNull();
        result.SafeMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PrepareAsync_WhenLinuxWorkspaceAccessReadOnly_BindsWorkspaceReadOnly()
    {
        var provider = LinuxProvider();

        var result = await provider.PrepareAsync(
            Intent(workspaceAccess: ProcessWorkspaceAccess.ReadOnly), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessSandboxStatus.Ready);
        var launch = result.Launch.ShouldNotBeNull();
        launch.ExecutablePath.ShouldBe(_bubblewrapPath);
        var arguments = launch.Arguments;
        var workspaceIndex = arguments.IndexOf(arguments.First(argument => argument.StartsWith(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal)));
        arguments[workspaceIndex - 1].ShouldBe("--ro-bind");
    }

    [Fact]
    public async Task PrepareAsync_WhenLinuxWorkspaceAccessReadWrite_BindsWorkspaceReadWrite()
    {
        var provider = LinuxProvider();

        var result = await provider.PrepareAsync(
            Intent(workspaceAccess: ProcessWorkspaceAccess.ReadWrite), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessSandboxStatus.Ready);
        var arguments = result.Launch.ShouldNotBeNull().Arguments;
        var workspaceIndex = arguments.IndexOf(arguments.First(argument => argument.StartsWith(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal)));
        arguments[workspaceIndex - 1].ShouldBe("--bind");
    }

    [Fact]
    public async Task PrepareAsync_WhenLinuxReadOnlyRootCaptured_AddsRoBindEntryForThatRoot()
    {
        var provider = LinuxProvider();
        var root = "/opt/agentkit-test-toolchain";

        var result = await provider.PrepareAsync(
            Intent(roots: [new ProcessReadOnlyRoot("test-toolchain", root)]), TestContext.Current.CancellationToken);

        var arguments = result.Launch.ShouldNotBeNull().Arguments;
        var rootIndex = arguments.IndexOf(root);
        rootIndex.ShouldBeGreaterThan(0);
        arguments[rootIndex - 1].ShouldBe("--ro-bind");
        arguments[rootIndex + 1].ShouldBe(root);
    }

    [Fact]
    public async Task PrepareAsync_WhenLinuxEnvironmentVariablesCaptured_AddsSetenvEntryForEach()
    {
        var provider = LinuxProvider();

        var result = await provider.PrepareAsync(
            Intent(environment: [new ProcessEnvironmentVariable("AGENTKIT_TEST", "value")]),
            TestContext.Current.CancellationToken);

        var arguments = result.Launch.ShouldNotBeNull().Arguments;
        var nameIndex = arguments.IndexOf("AGENTKIT_TEST");
        nameIndex.ShouldBeGreaterThan(0);
        arguments[nameIndex - 1].ShouldBe("--setenv");
        arguments[nameIndex + 1].ShouldBe("value");
    }

    [Fact]
    public async Task PrepareAsync_WhenLinuxOptionalBindDirectoriesAbsent_OmitsThemFromArguments()
    {
        var provider = new PlatformProcessSandboxProvider(new FakeProcessSandboxPlatformProbe(
            isLinux: true,
            fileExists: path => path == _bubblewrapPath,
            directoryExists: static _ => false));

        var result = await provider.PrepareAsync(Intent(), TestContext.Current.CancellationToken);

        var arguments = result.Launch.ShouldNotBeNull().Arguments;
        arguments.ShouldNotContain("/usr");
        arguments.ShouldNotContain("/etc");
    }

    [Fact]
    public async Task PrepareAsync_WhenLinuxOptionalBindDirectoriesPresent_AddsRoBindEntryForEach()
    {
        var provider = new PlatformProcessSandboxProvider(new FakeProcessSandboxPlatformProbe(
            isLinux: true,
            fileExists: path => path == _bubblewrapPath,
            directoryExists: static _ => true));

        var result = await provider.PrepareAsync(Intent(), TestContext.Current.CancellationToken);

        var arguments = result.Launch.ShouldNotBeNull().Arguments;
        foreach (var path in new[] { "/usr", "/bin", "/sbin", "/lib", "/lib64", "/etc" })
        {
            var index = arguments.IndexOf(path);
            index.ShouldBeGreaterThan(0);
            arguments[index - 1].ShouldBe("--ro-bind");
        }
    }

    private static PlatformProcessSandboxProvider MacOsProvider() => new(
        new FakeProcessSandboxPlatformProbe(isMacOs: true, fileExists: path => path == _sandboxExecPath));

    private static PlatformProcessSandboxProvider LinuxProvider() => new(
        new FakeProcessSandboxPlatformProbe(isLinux: true, fileExists: path => path == _bubblewrapPath));

    private static ResolvedProcessIntent Intent(
        ImmutableArray<ProcessReadOnlyRoot> roots = default,
        ProcessWorkspaceAccess workspaceAccess = ProcessWorkspaceAccess.ReadOnly,
        ProcessChildPolicy childPolicy = ProcessChildPolicy.AllowSandboxed,
        ImmutableArray<ProcessEnvironmentVariable> environment = default)
    {
        var request = new ProcessResolveRequest(
            new ProcessOperationId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            "/bin/sh",
            ["-c", "true"],
            null,
            environment.IsDefault ? [] : environment,
            [],
            PlatformProcessSandboxProvider.WorkspaceNoNetworkProfile,
            workspaceAccess,
            ProcessSideEffectClass.ReadOnly,
            childPolicy,
            new ProcessResourceLimits(TimeSpan.FromSeconds(1), 1024, TimeSpan.FromMilliseconds(100)))
        {
            ReadOnlyRoots = roots.IsDefault ? [] : roots,
        };
        return new ResolvedProcessIntent(
            request,
            "/bin/sh",
            new ContentHash("sha256:executable"),
            Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
            Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
            new ContentHash("sha256:environment"),
            new ContentHash("sha256:input"));
    }
}
