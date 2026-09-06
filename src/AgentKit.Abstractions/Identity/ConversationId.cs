// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies an optional higher-level conversation that groups one or more
/// <see cref="SessionId"/> values, such as a multi-session support thread or
/// a workspace-level chat that spans several agent runs.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. Two instances wrapping the same GUID are always
/// equal, and instances carry no mutable state, so they are safe to share
/// and compare across threads without synchronization.
/// </para>
/// <para>
/// Unlike most AgentKit identities, callers commonly work with an optional
/// <see cref="ConversationId"/><c>?</c> rather than a required value,
/// because not every session belongs to a broader conversation. When a
/// <see cref="ConversationId"/> value is present, however, it must still be
/// non-default: this constructor rejects <see cref="Guid.Empty"/> so an
/// explicitly supplied conversation grouping always addresses something
/// real, even though "no grouping" is legitimately expressed as
/// <see langword="null"/> rather than a zero GUID.
/// </para>
/// </remarks>
public readonly record struct ConversationId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationId"/>
    /// struct, validating that it addresses a real conversation grouping
    /// rather than an empty placeholder.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>. Use a
    /// <see langword="null"/> <see cref="Nullable{ConversationId}"/> to
    /// represent "no conversation grouping" instead of an empty GUID.
    /// </exception>
    public ConversationId(Guid value)
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
