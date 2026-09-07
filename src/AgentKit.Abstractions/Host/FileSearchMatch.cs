// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one bounded match against one exact observed file version.</summary>
public sealed record FileSearchMatch
{
    /// <summary>Initializes a search match.</summary>
    /// <param name="path">The canonical workspace-relative path.</param>
    /// <param name="contentFingerprint">The algorithm-qualified fingerprint of the complete observed file bytes.</param>
    /// <param name="lineNumber">The one-based line number.</param>
    /// <param name="lineByteOffset">The zero-based UTF-8 byte offset of the line in the file.</param>
    /// <param name="matchByteOffset">The zero-based UTF-8 byte offset of the match within the line.</param>
    /// <param name="matchByteLength">The UTF-8 byte length of the match.</param>
    /// <param name="lineProjectionByteOffset">The zero-based byte offset where the retained line projection begins.</param>
    /// <param name="lineText">The bounded decoded line projection.</param>
    /// <param name="lineTextTruncated">Whether the projection omits line bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">A position is outside its valid range.</exception>
    /// <exception cref="ArgumentException"><paramref name="contentFingerprint"/> or <paramref name="lineText"/> is blank.</exception>
    public FileSearchMatch(
        FileSystemPath path,
        ContentHash contentFingerprint,
        int lineNumber,
        long lineByteOffset,
        int matchByteOffset,
        int matchByteLength,
        int lineProjectionByteOffset,
        string lineText,
        bool lineTextTruncated)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentFingerprint.Value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineNumber);
        ArgumentOutOfRangeException.ThrowIfNegative(lineByteOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(matchByteOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(matchByteLength);
        ArgumentOutOfRangeException.ThrowIfNegative(lineProjectionByteOffset);
        ArgumentNullException.ThrowIfNull(lineText);
        Path = path;
        ContentFingerprint = contentFingerprint;
        LineNumber = lineNumber;
        LineByteOffset = lineByteOffset;
        MatchByteOffset = matchByteOffset;
        MatchByteLength = matchByteLength;
        LineProjectionByteOffset = lineProjectionByteOffset;
        LineText = lineText;
        LineTextTruncated = lineTextTruncated;
    }

    /// <summary>Gets the canonical path.</summary>
    public FileSystemPath Path { get; init; }
    /// <summary>Gets the observed full-content fingerprint.</summary>
    public ContentHash ContentFingerprint { get; init; }
    /// <summary>Gets the one-based line number.</summary>
    public int LineNumber { get; init; }
    /// <summary>Gets the zero-based byte offset of the line.</summary>
    public long LineByteOffset { get; init; }
    /// <summary>Gets the zero-based byte offset of the match within the line.</summary>
    public int MatchByteOffset { get; init; }
    /// <summary>Gets the byte length of the match.</summary>
    public int MatchByteLength { get; init; }
    /// <summary>Gets the byte offset where the retained line projection begins.</summary>
    public int LineProjectionByteOffset { get; init; }
    /// <summary>Gets the bounded decoded line projection.</summary>
    public string LineText { get; init; }
    /// <summary>Gets whether the line projection is truncated.</summary>
    public bool LineTextTruncated { get; init; }
}
