// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

using System.Runtime.InteropServices;

/// <summary>Verifies POSIX open-flag selection for the architectures the sandbox runs on.</summary>
public sealed class PosixOpenFlagsTests
{
    [Theory]
    [InlineData(Architecture.X64)]
    [InlineData(Architecture.Arm64)]
    [InlineData(Architecture.Arm)]
    public void ResolveDirectory_WhenMacOs_UsesMacOsConstantRegardlessOfArchitecture(Architecture architecture)
    {
        PosixOpenFlags.ResolveDirectory(isMacOs: true, isLinux: false, architecture)
            .ShouldBe(PosixOpenFlags.MacOsDirectory);
        PosixOpenFlags.ResolveNoFollow(isMacOs: true, isLinux: false, architecture)
            .ShouldBe(PosixOpenFlags.MacOsNoFollow);
    }

    [Fact]
    public void ResolveDirectory_WhenLinuxArm64_UsesAarch64Constants()
    {
        PosixOpenFlags.ResolveDirectory(isMacOs: false, isLinux: true, Architecture.Arm64)
            .ShouldBe(PosixOpenFlags.LinuxArm64Directory);
        PosixOpenFlags.ResolveNoFollow(isMacOs: false, isLinux: true, Architecture.Arm64)
            .ShouldBe(PosixOpenFlags.LinuxArm64NoFollow);
    }

    [Theory]
    [InlineData(Architecture.X64)]
    [InlineData(Architecture.X86)]
    [InlineData(Architecture.Arm)]
    [InlineData(Architecture.RiscV64)]
    public void ResolveDirectory_WhenLinuxIsNotArm64_UsesX64Constants(Architecture architecture)
    {
        PosixOpenFlags.ResolveDirectory(isMacOs: false, isLinux: true, architecture)
            .ShouldBe(PosixOpenFlags.LinuxX64Directory);
        PosixOpenFlags.ResolveNoFollow(isMacOs: false, isLinux: true, architecture)
            .ShouldBe(PosixOpenFlags.LinuxX64NoFollow);
    }

    [Fact]
    public void ResolveDirectory_WhenMacOsAndLinuxFlagsAreBothSet_PrefersMacOs()
    {
        PosixOpenFlags.ResolveDirectory(isMacOs: true, isLinux: true, Architecture.Arm64)
            .ShouldBe(PosixOpenFlags.MacOsDirectory);
        PosixOpenFlags.ResolveNoFollow(isMacOs: true, isLinux: true, Architecture.Arm64)
            .ShouldBe(PosixOpenFlags.MacOsNoFollow);
    }

    [Fact]
    public void Directory_WhenCurrentProcess_MatchesTheResolvedTable()
    {
        var architecture = RuntimeInformation.ProcessArchitecture;
        PosixOpenFlags.Directory.ShouldBe(
            PosixOpenFlags.ResolveDirectory(OperatingSystem.IsMacOS(), OperatingSystem.IsLinux(), architecture));
        PosixOpenFlags.NoFollow.ShouldBe(
            PosixOpenFlags.ResolveNoFollow(OperatingSystem.IsMacOS(), OperatingSystem.IsLinux(), architecture));
    }
}
