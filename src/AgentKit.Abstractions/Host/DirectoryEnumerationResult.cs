// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a typed directory outcome and, on success, one stable deterministic page.</summary>
public sealed record DirectoryEnumerationResult
{
    /// <summary>Initializes a directory-enumeration result.</summary>
    /// <param name="status">The terminal status.</param>
    /// <param name="entries">The ordered page entries; empty for non-success outcomes.</param>
    /// <param name="snapshotFingerprint">The complete ordered snapshot fingerprint on success.</param>
    /// <param name="continuation">The next-page cursor, or null when the snapshot is exhausted.</param>
    /// <param name="safeMessage">A non-sensitive explanation for non-success outcomes.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="entries"/> is default, or a non-success result has no safe message.</exception>
    public DirectoryEnumerationResult(
        DirectoryEnumerationStatus status,
        ImmutableArray<DirectoryEntry> entries,
        ContentHash? snapshotFingerprint,
        DirectoryEnumerationCursor? continuation,
        string? safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentException.ThrowIfDefault(entries);
        if (status != DirectoryEnumerationStatus.Success)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        }

        Status = status;
        Entries = entries;
        SnapshotFingerprint = snapshotFingerprint;
        Continuation = continuation;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the terminal status.</summary>
    public DirectoryEnumerationStatus Status { get; init; }
    /// <summary>Gets the deterministic page entries.</summary>
    public ImmutableArray<DirectoryEntry> Entries { get; init; }
    /// <summary>Gets the complete ordered snapshot fingerprint on success.</summary>
    public ContentHash? SnapshotFingerprint { get; init; }
    /// <summary>Gets the next-page cursor when more entries remain.</summary>
    public DirectoryEnumerationCursor? Continuation { get; init; }
    /// <summary>Gets the non-sensitive failure explanation.</summary>
    public string? SafeMessage { get; init; }

    /// <inheritdoc/>
    public bool Equals(DirectoryEnumerationResult? other) =>
        other is not null
        && Status == other.Status
        && Entries.SequenceEqual(other.Entries)
        && SnapshotFingerprint == other.SnapshotFingerprint
        && Continuation == other.Continuation
        && SafeMessage == other.SafeMessage;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Status);
        foreach (var entry in Entries)
        {
            hash.Add(entry);
        }

        hash.Add(SnapshotFingerprint);
        hash.Add(Continuation);
        hash.Add(SafeMessage);
        return hash.ToHashCode();
    }
}
