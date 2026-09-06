// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one durable conversation session owned by an
/// <see cref="AgentId"/>. A session is the append-only container for a
/// conversation's message history, branches, and admitted-input state.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>: two instances wrapping the same GUID are always
/// equal. Instances carry no mutable state, so they are safe to share,
/// compare, and use as lookup keys from multiple threads simultaneously.
/// </para>
/// <para>
/// As with every AgentKit identity struct, a default-initialized value (an
/// all-zero GUID) can technically exist in memory because the CLR always
/// permits a struct's default value. This constructor closes that gap for
/// every explicitly constructed, parsed, or deserialized instance by
/// rejecting <see cref="Guid.Empty"/>, so a valid <see cref="SessionId"/>
/// is always resolvable to a real session.
/// </para>
/// <para>
/// A <see cref="SessionId"/> is normally created once when a session is
/// opened and then reused for the lifetime of that conversation across many
/// runs; it does not change when the session's active branch, version, or
/// sequence advances. See <see cref="BranchId"/>, <see cref="SessionVersion"/>,
/// and <see cref="SessionSequence"/> for the values that do change as a
/// session's history grows.
/// </para>
/// </remarks>
public readonly record struct SessionId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionId"/> struct,
    /// validating that it addresses a real session rather than an empty
    /// placeholder.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which can never
    /// address a real session.
    /// </exception>
    public SessionId(Guid value)
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
