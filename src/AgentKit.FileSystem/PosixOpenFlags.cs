// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

using System.Runtime.InteropServices;

/// <summary>
/// Selects the host <c>open</c> flags that keep sandboxed traversal from following
/// symbolic links or treating a non-directory as a directory.
/// </summary>
/// <remarks>
/// <para>
/// Linux does not use one <c>O_DIRECTORY</c> / <c>O_NOFOLLOW</c> value on every
/// architecture. x86_64 uses <c>0x10000</c> and <c>0x20000</c>; aarch64 uses
/// <c>0x4000</c> and <c>0x8000</c>. macOS uses a third pair. Passing the x86_64
/// constants on aarch64 Linux does not reject symlinks, so containment checks
/// that depend on <c>O_NOFOLLOW</c> would follow a link out of the root.
/// </para>
/// <para>
/// The process-facing properties read the current OS and architecture once per
/// call. <see cref="ResolveDirectory"/> and <see cref="ResolveNoFollow"/> exist
/// so tests can prove every branch without running on that host. Non-macOS
/// architectures other than Linux arm64 keep the historical x86_64 constants,
/// which also match Linux riscv64. Those flags are only passed to <c>open</c>
/// on Linux and macOS.
/// </para>
/// </remarks>
internal static class PosixOpenFlags
{
    /// <summary>macOS <c>O_DIRECTORY</c>.</summary>
    internal const int MacOsDirectory = 0x00100000;

    /// <summary>Linux x86_64 <c>O_DIRECTORY</c>.</summary>
    internal const int LinuxX64Directory = 0x00010000;

    /// <summary>Linux aarch64 <c>O_DIRECTORY</c>.</summary>
    internal const int LinuxArm64Directory = 0x4000;

    /// <summary>macOS <c>O_NOFOLLOW</c>.</summary>
    internal const int MacOsNoFollow = 0x0100;

    /// <summary>Linux x86_64 <c>O_NOFOLLOW</c>.</summary>
    internal const int LinuxX64NoFollow = 0x00020000;

    /// <summary>Linux aarch64 <c>O_NOFOLLOW</c>.</summary>
    internal const int LinuxArm64NoFollow = 0x8000;

    /// <summary>Gets <c>O_DIRECTORY</c> for the current process.</summary>
    internal static int Directory =>
        ResolveDirectory(
            OperatingSystem.IsMacOS(),
            OperatingSystem.IsLinux(),
            RuntimeInformation.ProcessArchitecture);

    /// <summary>Gets <c>O_NOFOLLOW</c> for the current process.</summary>
    internal static int NoFollow =>
        ResolveNoFollow(
            OperatingSystem.IsMacOS(),
            OperatingSystem.IsLinux(),
            RuntimeInformation.ProcessArchitecture);

    /// <summary>Selects <c>O_DIRECTORY</c> for an explicit OS and architecture.</summary>
    /// <param name="isMacOs">Whether the host is macOS. This wins over Linux arm64.</param>
    /// <param name="isLinux">Whether the host is Linux.</param>
    /// <param name="architecture">The process architecture.</param>
    /// <returns>The flag value the sandboxed file system must pass to <c>open</c>.</returns>
    internal static int ResolveDirectory(bool isMacOs, bool isLinux, Architecture architecture) =>
        isMacOs
            ? MacOsDirectory
            : isLinux && architecture == Architecture.Arm64
                ? LinuxArm64Directory
                : LinuxX64Directory;

    /// <summary>Selects <c>O_NOFOLLOW</c> for an explicit OS and architecture.</summary>
    /// <param name="isMacOs">Whether the host is macOS. This wins over Linux arm64.</param>
    /// <param name="isLinux">Whether the host is Linux.</param>
    /// <param name="architecture">The process architecture.</param>
    /// <returns>The flag value the sandboxed file system must pass to <c>open</c>.</returns>
    internal static int ResolveNoFollow(bool isMacOs, bool isLinux, Architecture architecture) =>
        isMacOs
            ? MacOsNoFollow
            : isLinux && architecture == Architecture.Arm64
                ? LinuxArm64NoFollow
                : LinuxX64NoFollow;
}
