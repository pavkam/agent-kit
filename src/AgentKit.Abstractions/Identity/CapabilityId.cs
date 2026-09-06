// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one named optional capability an agent definition may
/// reference through an <see cref="AgentCapabilityReference"/>, such as a
/// durability, memory, or goal axis that is not part of the mandatory
/// runnable spine every agent must select.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// Optional capabilities are demonstrated extension axes, not a generic
/// escape hatch: once a definition references a <see cref="CapabilityId"/>,
/// composition validation requires that capability's full set of required
/// collaborators — store, security policy, execution pipeline — to also be
/// registered and resolvable, exactly as if the capability were mandatory.
/// A capability identifier by itself never grants authority to perform the
/// operations it names.
/// </para>
/// </remarks>
public readonly record struct CapabilityId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CapabilityId"/> struct,
    /// validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty canonical capability identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public CapabilityId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical capability identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical identifier text.</summary>
    public override string ToString() => Value;
}
