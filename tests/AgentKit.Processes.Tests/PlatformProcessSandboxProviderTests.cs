// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Tests;

/// <summary>Verifies PlatformProcessSandboxProvider behavior and contracts.</summary>
public sealed class PlatformProcessSandboxProviderTests
{
    [Fact]
    public async Task PrepareAsync_WhenReadOnlyRootCaptured_ProjectsOnlyThatExternalRootReadOnly()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            return;
        }

        var provider = new PlatformProcessSandboxProvider();
        var root = "/opt/agentkit-test-toolchain";
        var intent = Intent([new ProcessReadOnlyRoot("test-toolchain", root)]);

        var result = await provider.PrepareAsync(intent, TestContext.Current.CancellationToken);

        if (result.Status == ProcessSandboxStatus.Unavailable)
        {
            return;
        }

        result.Status.ShouldBe(ProcessSandboxStatus.Ready);
        string.Join('\n', result.Launch.ShouldNotBeNull().Arguments).ShouldContain(root);
        string.Join('\n', (await provider.PrepareAsync(Intent([]), TestContext.Current.CancellationToken))
            .Launch.ShouldNotBeNull().Arguments).ShouldNotContain(root);
    }

    private static ResolvedProcessIntent Intent(ImmutableArray<ProcessReadOnlyRoot> roots)
    {
        var request = new ProcessResolveRequest(
            new ProcessOperationId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            "/bin/sh",
            ["-c", "true"],
            null,
            [],
            [],
            PlatformProcessSandboxProvider.WorkspaceNoNetworkProfile,
            ProcessWorkspaceAccess.ReadOnly,
            ProcessSideEffectClass.ReadOnly,
            ProcessChildPolicy.AllowSandboxed,
            new ProcessResourceLimits(TimeSpan.FromSeconds(1), 1024, TimeSpan.FromMilliseconds(100)))
        {
            ReadOnlyRoots = roots,
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
