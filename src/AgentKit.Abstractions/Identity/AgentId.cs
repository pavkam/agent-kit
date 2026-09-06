// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one agent definition catalogued by the engine, and every
/// durable record (messages, sessions, runs) that belongs to it. The
/// definition catalog, engine, and agent handle types that resolve this
/// identity to a runnable configuration live in the downstream AgentKit
/// facade package, not in AgentKit.Abstractions.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object. Equality, hashing, and ordering
/// are defined structurally over <see cref="Value"/>: two instances wrapping
/// the same GUID always compare equal, regardless of when or where they were
/// created. Instances are safe to share, compare, and use as dictionary keys
/// across threads without synchronization, because nothing about them can
/// change after construction.
/// </para>
/// <para>
/// Because <see cref="AgentId"/> is a struct, a default-initialized value
/// (an all-zero GUID) can still exist in memory — for example as the
/// implicit default of an uninitialized field. This constructor rejects that
/// value explicitly, so every <see cref="AgentId"/> that survives validated
/// construction, configuration binding, parsing, or deserialization is a
/// genuine, resolvable catalog address rather than an accidental default.
/// </para>
/// <para>
/// One engine can host many agent definitions concurrently; this identity is
/// what every component uses to select which definition, session, and run
/// state a piece of work belongs to instead of relying on an ambient
/// "current agent."
/// </para>
/// </remarks>
public readonly record struct AgentId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentId"/> struct,
    /// validating that it addresses a real catalog entry rather than an
    /// empty placeholder.
    /// </summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which can never
    /// address a real agent definition.
    /// </exception>
    public AgentId(Guid value)
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
