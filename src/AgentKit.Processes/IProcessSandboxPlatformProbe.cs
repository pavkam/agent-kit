// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>
/// Abstracts the operating-system identity and file-system presence checks that
/// <see cref="PlatformProcessSandboxProvider"/> consults when selecting and preparing a platform sandbox launch.
/// </summary>
/// <remarks>
/// Production code always resolves the real host through <see cref="SystemProcessSandboxPlatformProbe"/>. The
/// seam exists so tests can simulate every platform branch (macOS, Linux, and unsupported) and every sandbox-tool
/// or bind-target presence outcome deterministically, without depending on which operating system or file layout
/// happens to host the test run.
/// </remarks>
internal interface IProcessSandboxPlatformProbe
{
    /// <summary>Gets a value indicating whether the current host identifies as macOS.</summary>
    public bool IsMacOs { get; }

    /// <summary>Gets a value indicating whether the current host identifies as Linux.</summary>
    public bool IsLinux { get; }

    /// <summary>Determines whether a file exists at <paramref name="path"/>.</summary>
    /// <param name="path">The absolute file path to probe.</param>
    /// <returns><see langword="true"/> when a file exists at <paramref name="path"/>.</returns>
    public bool FileExists(string path);

    /// <summary>Determines whether a directory exists at <paramref name="path"/>.</summary>
    /// <param name="path">The absolute directory path to probe.</param>
    /// <returns><see langword="true"/> when a directory exists at <paramref name="path"/>.</returns>
    public bool DirectoryExists(string path);
}
