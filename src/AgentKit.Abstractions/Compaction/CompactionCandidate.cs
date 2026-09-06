// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A produced, not-yet-activated compaction candidate: its structural
/// manifest paired with its checkpoint content.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. A
/// candidate becomes an active <see cref="CompactionRecord"/> only after it
/// passes <see cref="ICompactionValidator"/> and is durably appended.
/// </remarks>
public sealed record CompactionCandidate
{
    /// <summary>Initializes a new instance of the <see cref="CompactionCandidate"/> record.</summary>
    /// <param name="manifest">The structural manifest for this candidate.</param>
    /// <param name="checkpoint">The checkpoint content for this candidate.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="manifest"/> or <paramref name="checkpoint"/> is null.
    /// </exception>
    public CompactionCandidate(CompactionManifest manifest, CompactionCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(checkpoint);

        Manifest = manifest;
        Checkpoint = checkpoint;
    }

    /// <summary>Gets the structural manifest for this candidate.</summary>
    public CompactionManifest Manifest { get; init; }

    /// <summary>Gets the checkpoint content for this candidate.</summary>
    public CompactionCheckpoint Checkpoint { get; init; }
}
