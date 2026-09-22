// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Write disposition and atomicity policy for one operating-system file profile.</summary>
public sealed record FileWritePolicy
{
    /// <summary>Initializes a new instance of the <see cref="FileWritePolicy"/> record.</summary>
    /// <param name="requiresAtomicAppend">Whether append must be non-interleaving.</param>
    public FileWritePolicy(bool requiresAtomicAppend = false) =>
        RequiresAtomicAppend = requiresAtomicAppend;

    /// <summary>Gets whether append must be non-interleaving.</summary>
    public bool RequiresAtomicAppend { get; init; }
}
