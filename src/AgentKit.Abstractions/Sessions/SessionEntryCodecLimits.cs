// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares finite limits a session-entry codec applies before parsing one
/// durable entry payload.
/// </summary>
/// <remarks>
/// The limits constrain one encoded entry, including its forward-compatible
/// extension data. They are immutable composition facts rather than caller
/// overrides, so a reader cannot allocate or traverse an unbounded persisted
/// value merely because a writer once accepted it.
/// </remarks>
public sealed record SessionEntryCodecLimits
{
    /// <summary>
    /// Initializes immutable parsing limits for one session-entry codec.
    /// </summary>
    /// <param name="maximumPayloadBytes">
    /// The positive maximum number of bytes in the type-specific encoded
    /// payload.
    /// </param>
    /// <param name="maximumExtensionCount">
    /// The positive maximum number of compatible unknown fields retained with
    /// one entry.
    /// </param>
    /// <param name="maximumExtensionBytes">
    /// The positive maximum combined byte length of retained compatible
    /// unknown fields.
    /// </param>
    /// <param name="maximumJsonDepth">
    /// The positive maximum JSON nesting depth accepted in the payload or a
    /// retained compatible field.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any limit is zero or negative.
    /// </exception>
    public SessionEntryCodecLimits(
        int maximumPayloadBytes,
        int maximumExtensionCount,
        int maximumExtensionBytes,
        int maximumJsonDepth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumPayloadBytes, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumExtensionCount, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumExtensionBytes, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumJsonDepth, 0);

        MaximumPayloadBytes = maximumPayloadBytes;
        MaximumExtensionCount = maximumExtensionCount;
        MaximumExtensionBytes = maximumExtensionBytes;
        MaximumJsonDepth = maximumJsonDepth;
    }

    /// <summary>
    /// Gets the positive maximum byte length of the type-specific payload.
    /// </summary>
    public int MaximumPayloadBytes { get; }

    /// <summary>
    /// Gets the positive maximum count of retained compatible unknown fields.
    /// </summary>
    public int MaximumExtensionCount { get; }

    /// <summary>
    /// Gets the positive maximum combined byte length of retained compatible
    /// unknown fields.
    /// </summary>
    public int MaximumExtensionBytes { get; }

    /// <summary>
    /// Gets the positive maximum nesting depth accepted by the codec's JSON
    /// representation.
    /// </summary>
    public int MaximumJsonDepth { get; }
}
