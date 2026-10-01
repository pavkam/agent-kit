// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Watch policy for a profile; it is disabled by default because no first-party profile exposes change observation.</summary>
public sealed record FileWatchPolicy
{
    /// <summary>Initializes a new instance of the <see cref="FileWatchPolicy"/> record.</summary>
    /// <param name="enabled">Whether watching is enabled for the profile.</param>
    public FileWatchPolicy(bool enabled = false) => Enabled = enabled;

    /// <summary>Gets whether watching is enabled for the profile.</summary>
    public bool Enabled { get; init; }
}
