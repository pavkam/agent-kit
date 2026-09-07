// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports whether an exact conditional replacement committed.</summary>
public sealed record AtomicFileReplaceResult
{
    /// <summary>Initializes an atomic replacement result.</summary>
    /// <param name="status">The terminal status.</param>
    /// <param name="contentFingerprint">The committed fingerprint when known.</param>
    /// <param name="bytes">The committed byte count, or zero before effect.</param>
    /// <param name="safeMessage">A non-sensitive explanation when present.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined or <paramref name="bytes"/> is negative.</exception>
    public AtomicFileReplaceResult(
        AtomicFileReplaceStatus status,
        ContentHash? contentFingerprint,
        long bytes,
        string? safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        Status = status;
        ContentFingerprint = contentFingerprint;
        Bytes = bytes;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the terminal status.</summary>
    public AtomicFileReplaceStatus Status { get; init; }
    /// <summary>Gets the committed content fingerprint when known.</summary>
    public ContentHash? ContentFingerprint { get; init; }
    /// <summary>Gets the committed byte count.</summary>
    public long Bytes { get; init; }
    /// <summary>Gets a non-sensitive explanation when present.</summary>
    public string? SafeMessage { get; init; }
}
