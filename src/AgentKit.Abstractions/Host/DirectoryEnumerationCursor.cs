// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resumes directory enumeration against one exact observed snapshot.</summary>
public sealed record DirectoryEnumerationCursor
{
    /// <summary>Initializes a stable directory cursor.</summary>
    /// <param name="snapshotFingerprint">The fingerprint of the complete ordered entry-name snapshot.</param>
    /// <param name="nextIndex">The positive zero-based index of the next entry.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nextIndex"/> is not positive.</exception>
    public DirectoryEnumerationCursor(ContentHash snapshotFingerprint, int nextIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nextIndex);
        SnapshotFingerprint = snapshotFingerprint;
        NextIndex = nextIndex;
    }

    /// <summary>Gets the snapshot fingerprint.</summary>
    public ContentHash SnapshotFingerprint { get; init; }
    /// <summary>Gets the zero-based index of the next entry.</summary>
    public int NextIndex { get; init; }
}
