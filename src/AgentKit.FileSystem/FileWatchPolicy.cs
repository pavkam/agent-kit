// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Placeholder watch policy for profiles that do not expose change observation.</summary>
public sealed record FileWatchPolicy
{
    /// <summary>Initializes a new instance of the <see cref="FileWatchPolicy"/> record.</summary>
    /// <param name="enabled">Whether watching is enabled for the profile.</param>
    public FileWatchPolicy(bool enabled = false) => Enabled = enabled;

    /// <summary>Gets whether watching is enabled for the profile.</summary>
    public bool Enabled { get; init; }
}
