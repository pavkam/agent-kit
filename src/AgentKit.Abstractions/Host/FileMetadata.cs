// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observed metadata for one regular file target at open time.</summary>
/// <remarks>
/// Length and timestamps describe external host truth at the observation
/// boundary. A complete-file fingerprint requires a stable byte snapshot and
/// is never inferred from a truncated prefix.
/// </remarks>
public sealed record FileMetadata
{
    /// <summary>Initializes a new instance of the <see cref="FileMetadata"/> record.</summary>
    /// <param name="lengthBytes">The observed content length in bytes.</param>
    /// <param name="lastModifiedUtc">The observed last-modified instant in UTC, if known.</param>
    /// <param name="contentFingerprint">
    /// A complete-file fingerprint when a stable snapshot exists; otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthBytes"/> is negative.</exception>
    public FileMetadata(long lengthBytes, DateTimeOffset? lastModifiedUtc, ContentHash? contentFingerprint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lengthBytes);
        LengthBytes = lengthBytes;
        LastModifiedUtc = lastModifiedUtc;
        ContentFingerprint = contentFingerprint;
    }

    /// <summary>Gets the observed content length in bytes.</summary>
    public long LengthBytes { get; init; }

    /// <summary>Gets the observed last-modified instant in UTC, if known.</summary>
    public DateTimeOffset? LastModifiedUtc { get; init; }

    /// <summary>Gets a complete-file fingerprint when a stable snapshot exists.</summary>
    public ContentHash? ContentFingerprint { get; init; }
}
