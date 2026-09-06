// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one <see cref="MediaReference"/> used by message content, such
/// as an image, audio clip, or file attachment carried by a
/// <see cref="MediaReferencePart"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state itself and is safe to
/// share and compare across threads without synchronization.
/// </para>
/// <para>
/// <see cref="MediaId"/> identifies the reference record — its source kind,
/// media type, and either inline bytes or a pointer to external content —
/// not the underlying bytes themselves. Two <see cref="MediaReference"/>
/// values that happen to point at byte-identical content still have
/// different <see cref="MediaId"/> values unless one was literally
/// constructed by copying the other; deduplication of identical bytes, when
/// needed, is a concern of whichever store or cache persists the media, not
/// of this identity.
/// </para>
/// <para>
/// As with every AgentKit identity struct, the CLR default value (an
/// all-zero GUID) can still exist as an uninitialized field's value; this
/// constructor rejects that value for every explicitly constructed instance
/// so a valid <see cref="MediaId"/> always addresses a real reference.
/// </para>
/// </remarks>
public readonly record struct MediaId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MediaId"/> struct,
    /// validating that it addresses a real media reference rather than an
    /// empty placeholder.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which can never
    /// address a real reference.
    /// </exception>
    public MediaId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>
    /// Gets the underlying globally unique identifier. This is the only
    /// state the type carries, and it never changes after construction.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Returns the canonical, lowercase, hyphenated text form of this
    /// identity (GUID "D" format), suitable for logging, storage keys, and
    /// round-tripping through configuration or external protocols.
    /// </summary>
    public override string ToString() => Value.ToString("D");
}
