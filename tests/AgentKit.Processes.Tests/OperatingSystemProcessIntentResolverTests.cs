// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Tests;



/// <summary>Verifies OperatingSystemProcessIntentResolver behavior and contracts.</summary>
public sealed class OperatingSystemProcessIntentResolverTests: IDisposable
{
    public OperatingSystemProcessIntentResolverTests() => _ = Directory.CreateDirectory(_root);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"agentkit-process-{Guid.NewGuid():N}");
    [Fact]
    public async Task ResolveAsync_WhenExecutableAndWorkingDirectoryAllowed_ReturnsCanonicalFingerprintedIntent()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        _ = Directory.CreateDirectory(Path.Combine(_root, "src"));
        var resolver = CreateResolver("/bin/sh");
        var request = Request("/bin/sh", ["-c", "printf ok"], new FileSystemPath("src"));
        var result = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessResolutionStatus.Resolved);
        var intent = result.Intent.ShouldNotBeNull();
        Path.IsPathRooted(intent.AbsoluteExecutablePath).ShouldBeTrue();
        intent.AbsoluteWorkingDirectory.ShouldBe(Path.Combine(intent.AbsoluteWorkspaceRoot, "src"));
        intent.ExecutableFingerprint.Value.ShouldStartWith("sha256:");
        intent.StandardInputFingerprint.ShouldBe(ProcessSecurityBinding.FingerprintBytes([]));
    }

    [Fact]
    public async Task ResolveAsync_WhenWorkingDirectoryTraversesSymlink_RejectsOutsideWorkspace()
    {
        if (!IsSupported())
        {
            return;
        }

        var outside = Path.Combine(Path.GetTempPath(), $"agentkit-process-outside-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(outside);
        try
        {
            _ = Directory.CreateSymbolicLink(Path.Combine(_root, "outside"), outside);
            var resolver = CreateResolver("/bin/sh");
            var result = await resolver.ResolveAsync(Request("/bin/sh", [], new FileSystemPath("outside")), TestContext.Current.CancellationToken);
            result.Status.ShouldBe(ProcessResolutionStatus.WorkingDirectoryRejected);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveAsync_WhenReadOnlyEffectRequestsWritableWorkspace_RejectsIntent()
    {
        if (!IsSupported())
        {
            return;
        }

        var result = await CreateResolver("/bin/sh").ResolveAsync(Request("/bin/sh", [], workspaceAccess: ProcessWorkspaceAccess.ReadWrite, sideEffectClass: ProcessSideEffectClass.ReadOnly), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessResolutionStatus.InvalidIntent);
    }

    [Fact]
    public async Task ResolveAsync_WhenEnvironmentExceedsByteBound_RejectsBeforeFingerprinting()
    {
        if (!IsSupported())
        {
            return;
        }

        var options = OptionsFor("/bin/sh", 1024);
        options.MaximumEnvironmentBytes = 3;
        options.AllowedEnvironmentVariableNames.Add("A");
        var resolver = new OperatingSystemProcessIntentResolver(Options.Create(options));
        var request = Request("/bin/sh", [], environment: [new ProcessEnvironmentVariable("A", "123")]);
        var result = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessResolutionStatus.InvalidIntent);
    }

    [Fact]
    public async Task ResolveAsync_WhenReadOnlyToolchainRootConfigured_CapturesCanonicalRootInSecurityEvidence()
    {
        if (!IsSupported())
        {
            return;
        }

        var toolchain = Path.Combine(_root, "toolchain");
        _ = Directory.CreateDirectory(toolchain);
        var options = OptionsFor("/bin/sh", 1024);
        options.ReadOnlyToolchainRoots.Add("test-toolchain", toolchain);
        var result = await new OperatingSystemProcessIntentResolver(Options.Create(options)).ResolveAsync(
            Request("/bin/sh", []),
            TestContext.Current.CancellationToken);

        var intent = result.Intent.ShouldNotBeNull();
        var capturedRoot = intent.Request.ReadOnlyRoots.ShouldHaveSingleItem();
        capturedRoot.ProfileId.ShouldBe("test-toolchain");
        Path.IsPathRooted(capturedRoot.AbsolutePath).ShouldBeTrue();
        ProcessSecurityBinding.Resources(intent).ShouldContain(
            new ProtectedResource(
                ProtectedResourceKind.Directory,
                $"readonly:test-toolchain:{capturedRoot.AbsolutePath}"));
    }

    [Fact]
    public async Task ResolveAsync_WhenCapturedReadOnlyRootsChange_RejectsRevalidation()
    {
        if (!IsSupported())
        {
            return;
        }

        var toolchain = Path.Combine(_root, "toolchain");
        var different = Path.Combine(_root, "different");
        _ = Directory.CreateDirectory(toolchain);
        _ = Directory.CreateDirectory(different);
        var options = OptionsFor("/bin/sh", 1024);
        options.ReadOnlyToolchainRoots.Add("test-toolchain", toolchain);
        var request = Request("/bin/sh", []) with
        {
            ReadOnlyRoots = [new ProcessReadOnlyRoot("test-toolchain", different)],
        };

        var result = await new OperatingSystemProcessIntentResolver(Options.Create(options)).ResolveAsync(
            request,
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessResolutionStatus.InvalidIntent);
    }

    [Fact]
    public void Constructor_WhenReadOnlyToolchainRootDoesNotExist_RejectsConfiguration()
    {
        if (!IsSupported())
        {
            return;
        }

        var options = OptionsFor("/bin/sh", 1024);
        options.ReadOnlyToolchainRoots.Add("missing", Path.Combine(_root, "missing"));

        _ = Should.Throw<ArgumentException>(() =>
            new OperatingSystemProcessIntentResolver(Options.Create(options)));
    }

    [Fact]
    public void Constructor_WhenReadOnlyToolchainRootIsAFile_RejectsConfiguration()
    {
        if (!IsSupported())
        {
            return;
        }

        var file = Path.Combine(_root, "toolchain-file");
        File.WriteAllText(file, "not a directory");
        var options = OptionsFor("/bin/sh", 1024);
        options.ReadOnlyToolchainRoots.Add("file", file);

        _ = Should.Throw<ArgumentException>(() =>
            new OperatingSystemProcessIntentResolver(Options.Create(options)));
    }

    [Fact]
    public async Task ResolveAsync_WhenToolchainSymlinkTargetChanges_RejectsCapturedIntent()
    {
        if (!IsSupported())
        {
            return;
        }

        var first = Path.Combine(_root, "toolchain-v1");
        var second = Path.Combine(_root, "toolchain-v2");
        var link = Path.Combine(_root, "toolchain-current");
        _ = Directory.CreateDirectory(first);
        _ = Directory.CreateDirectory(second);
        _ = Directory.CreateSymbolicLink(link, first);
        var options = OptionsFor("/bin/sh", 1024);
        options.ReadOnlyToolchainRoots.Add("toolchain", link);
        var resolver = new OperatingSystemProcessIntentResolver(Options.Create(options));
        var captured = (await resolver.ResolveAsync(Request("/bin/sh", []), TestContext.Current.CancellationToken))
            .Intent.ShouldNotBeNull();
        Directory.Delete(link);
        _ = Directory.CreateSymbolicLink(link, second);
        var result = await resolver.ResolveAsync(captured.Request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessResolutionStatus.InvalidIntent);
    }

    private OperatingSystemProcessIntentResolver CreateResolver(string executable, long maximumOutputBytes = 1024) => new(Options.Create(OptionsFor(executable, maximumOutputBytes)));
    private OperatingSystemProcessOptions OptionsFor(string executable, long maximumOutputBytes, long maximumArtifactOutputBytes = 64 * 1024 * 1024)
    {
        var options = new OperatingSystemProcessOptions
        {
            RootDirectory = _root,
            MaximumOutputBytes = maximumOutputBytes,
            MaximumArtifactOutputBytes = maximumArtifactOutputBytes,
            MaximumTimeout = TimeSpan.FromSeconds(10),
        };
        options.AllowedExecutablePaths.Add(executable);
        return options;
    }

    private static ProcessResolveRequest Request(string executable, ImmutableArray<string> arguments, FileSystemPath? workingDirectory = null, ProcessWorkspaceAccess workspaceAccess = ProcessWorkspaceAccess.ReadWrite, ProcessSideEffectClass sideEffectClass = ProcessSideEffectClass.WorkspaceMutation, long maximumOutputBytes = 1024, TimeSpan? timeout = null, TimeSpan? grace = null, ImmutableArray<ProcessEnvironmentVariable> environment = default, SandboxProfileId? sandboxProfile = null, ProcessChildPolicy childPolicy = ProcessChildPolicy.AllowSandboxed) => new(new ProcessOperationId(Guid.NewGuid()), executable, arguments, workingDirectory, environment.IsDefault ? [] : environment, [], sandboxProfile ?? PlatformProcessSandboxProvider.WorkspaceNoNetworkProfile, workspaceAccess, sideEffectClass, childPolicy, new ProcessResourceLimits(timeout ?? TimeSpan.FromSeconds(2), maximumOutputBytes, grace ?? TimeSpan.FromMilliseconds(250)));
    private static bool IsSupported() => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
