// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the exact settlement of one source-ordered patch entry.</summary>
public sealed record WorkspacePatchEntryResult
{
    /// <summary>Initializes an entry result.</summary>
    /// <param name="index">The zero-based source ordinal.</param>
    /// <param name="kind">The planned entry kind.</param>
    /// <param name="status">The settled entry state.</param>
    /// <param name="sourcePath">The source or target path.</param>
    /// <param name="destinationPath">The move destination, when applicable.</param>
    /// <param name="contentFingerprint">The committed content fingerprint when applicable and known.</param>
    /// <param name="safeMessage">A non-sensitive explanation when present.</param>
    /// <exception cref="ArgumentOutOfRangeException">An ordinal or enum is invalid.</exception>
    public WorkspacePatchEntryResult(
        int index,
        WorkspacePatchEntryKind kind,
        WorkspacePatchEntryStatus status,
        FileSystemPath sourcePath,
        FileSystemPath? destinationPath,
        ContentHash? contentFingerprint,
        string? safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        Index = index;
        Kind = kind;
        Status = status;
        SourcePath = sourcePath;
        DestinationPath = destinationPath;
        ContentFingerprint = contentFingerprint;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the source ordinal.</summary>
    public int Index { get; init; }
    /// <summary>Gets the planned entry kind.</summary>
    public WorkspacePatchEntryKind Kind { get; init; }
    /// <summary>Gets the settled entry state.</summary>
    public WorkspacePatchEntryStatus Status { get; init; }
    /// <summary>Gets the source or target path.</summary>
    public FileSystemPath SourcePath { get; init; }
    /// <summary>Gets the move destination when applicable.</summary>
    public FileSystemPath? DestinationPath { get; init; }
    /// <summary>Gets the committed content fingerprint when known.</summary>
    public ContentHash? ContentFingerprint { get; init; }
    /// <summary>Gets a non-sensitive explanation when present.</summary>
    public string? SafeMessage { get; init; }
}
