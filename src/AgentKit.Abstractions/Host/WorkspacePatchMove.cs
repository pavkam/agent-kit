// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Moves one exact existing file version to an absent destination.</summary>
public sealed record WorkspacePatchMove: WorkspacePatchEntry
{
    /// <summary>Initializes an exact move entry.</summary>
    /// <param name="id">The mutation identity.</param>
    /// <param name="sourcePath">The existing source.</param>
    /// <param name="destinationPath">The destination that must be absent.</param>
    /// <param name="expectedContentFingerprint">The required current source fingerprint.</param>
    /// <param name="grant">The move authority over both paths.</param>
    /// <exception cref="ArgumentOutOfRangeException">Source and destination are equal.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public WorkspacePatchMove(
        WorkspaceMutationId id,
        FileSystemPath sourcePath,
        FileSystemPath destinationPath,
        ContentHash expectedContentFingerprint,
        SecurityGrant grant) : base(id, WorkspacePatchEntryKind.Move, grant)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(destinationPath, sourcePath, nameof(destinationPath));

        SourcePath = sourcePath;
        DestinationPath = destinationPath;
        ExpectedContentFingerprint = expectedContentFingerprint;
    }

    /// <summary>Gets the existing source path.</summary>
    public FileSystemPath SourcePath { get; init; }
    /// <summary>Gets the destination that must be absent.</summary>
    public FileSystemPath DestinationPath { get; init; }
    /// <summary>Gets the required current source fingerprint.</summary>
    public ContentHash ExpectedContentFingerprint { get; init; }
}
