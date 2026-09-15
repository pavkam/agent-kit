// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one host-configured absolute directory exposed read-only to a sandboxed process.</summary>
public sealed record ProcessReadOnlyRoot
{
    /// <summary>Initializes one captured read-only root.</summary>
    /// <param name="profileId">The stable host-authored profile identity.</param>
    /// <param name="absolutePath">The lexically absolute directory path; the process resolver later requires a canonical existing directory.</param>
    /// <exception cref="ArgumentException">The identity is blank or the path is blank or relative.</exception>
    public ProcessReadOnlyRoot(string profileId, string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        ArgumentException.ThrowIfPathNotRooted(absolutePath);

        ProfileId = profileId;
        AbsolutePath = absolutePath;
    }

    /// <summary>Gets the stable host-authored profile identity.</summary>
    public string ProfileId { get; }

    /// <summary>Gets the lexically absolute path that a process resolver must canonicalize before use.</summary>
    public string AbsolutePath { get; }
}
