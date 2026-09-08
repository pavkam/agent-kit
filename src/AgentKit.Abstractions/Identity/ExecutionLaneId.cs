// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one independently owned execution lane within a session. A lane
/// scopes the active operation and pending input that may progress together.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state and is safe to share,
/// compare, and use as a lookup key across threads without synchronization.
/// </para>
/// <para>
/// A default-initialized value can exist because this is a struct, but its
/// all-zero GUID cannot identify a lane. The constructor rejects that value,
/// so identities created at caller, configuration, parsing, and deserialization
/// boundaries have a valid non-empty identity shape. A session lookup proves
/// whether the identity addresses an installed lane.
/// </para>
/// <para>
/// A lane identity is distinct from any display name a host assigns to the
/// lane. It grants neither authority nor access to another lane's work.
/// </para>
/// </remarks>
public readonly record struct ExecutionLaneId
{
    /// <summary>
    /// Initializes a non-empty execution-lane identity rather than an empty
    /// placeholder. A session lookup establishes whether it is installed.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which can never
    /// identify an installed execution lane.
    /// </exception>
    public ExecutionLaneId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>
    /// Gets the underlying globally unique identifier. This is the only state
    /// the type carries, and it never changes after construction.
    /// </summary>
    /// <value>A non-empty GUID whose structural equality defines the lane identity.</value>
    public Guid Value { get; }

    /// <summary>
    /// Returns the canonical, lowercase, hyphenated text form of this identity
    /// (GUID "D" format), suitable for logging, storage keys, and round-tripping
    /// through configuration or external protocols.
    /// </summary>
    /// <returns>The immutable <see cref="Value"/> in GUID "D" format.</returns>
    public override string ToString() => Value.ToString("D");
}
