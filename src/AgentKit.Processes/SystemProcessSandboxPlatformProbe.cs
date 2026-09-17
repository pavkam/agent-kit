// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>The default <see cref="IProcessSandboxPlatformProbe"/> that consults the real host operating system and file system.</summary>
internal sealed class SystemProcessSandboxPlatformProbe: IProcessSandboxPlatformProbe
{
    /// <summary>Gets the shared stateless instance.</summary>
    public static readonly SystemProcessSandboxPlatformProbe Instance = new();

    private SystemProcessSandboxPlatformProbe()
    {
    }

    /// <inheritdoc/>
    public bool IsMacOs => OperatingSystem.IsMacOS();

    /// <inheritdoc/>
    public bool IsLinux => OperatingSystem.IsLinux();

    /// <inheritdoc/>
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc/>
    public bool DirectoryExists(string path) => Directory.Exists(path);
}
