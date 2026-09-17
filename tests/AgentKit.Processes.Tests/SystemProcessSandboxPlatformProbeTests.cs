// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Tests;

/// <summary>Verifies SystemProcessSandboxPlatformProbe behavior and contracts.</summary>
public sealed class SystemProcessSandboxPlatformProbeTests
{
    [Fact]
    public void Instance_WhenRead_ReturnsTheSameSharedProbe() =>
        SystemProcessSandboxPlatformProbe.Instance.ShouldBeSameAs(SystemProcessSandboxPlatformProbe.Instance);

    [Fact]
    public void IsMacOs_WhenRead_MatchesOperatingSystemIsMacOS() =>
        SystemProcessSandboxPlatformProbe.Instance.IsMacOs.ShouldBe(OperatingSystem.IsMacOS());

    [Fact]
    public void IsLinux_WhenRead_MatchesOperatingSystemIsLinux() =>
        SystemProcessSandboxPlatformProbe.Instance.IsLinux.ShouldBe(OperatingSystem.IsLinux());

    [Fact]
    public void FileExists_WhenPathIsAnExistingFile_ReturnsTrue()
    {
        var path = Path.GetTempFileName();
        try
        {
            SystemProcessSandboxPlatformProbe.Instance.FileExists(path).ShouldBeTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FileExists_WhenPathDoesNotExist_ReturnsFalse() =>
        SystemProcessSandboxPlatformProbe.Instance.FileExists(
            Path.Combine(Path.GetTempPath(), $"agentkit-missing-{Guid.NewGuid():N}")).ShouldBeFalse();

    [Fact]
    public void DirectoryExists_WhenPathIsAnExistingDirectory_ReturnsTrue() =>
        SystemProcessSandboxPlatformProbe.Instance.DirectoryExists(Path.GetTempPath()).ShouldBeTrue();

    [Fact]
    public void DirectoryExists_WhenPathDoesNotExist_ReturnsFalse() =>
        SystemProcessSandboxPlatformProbe.Instance.DirectoryExists(
            Path.Combine(Path.GetTempPath(), $"agentkit-missing-{Guid.NewGuid():N}")).ShouldBeFalse();
}
