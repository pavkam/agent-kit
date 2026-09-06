// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one configured profile for a <see cref="CapabilityId"/> — the
/// specific bundle of options, store, and policy an agent definition wants
/// when it references that capability, as opposed to some other profile of
/// the same capability configured for a different agent.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// Separating "which capability" (<see cref="CapabilityId"/>) from "which
/// configured profile of it" (<see cref="CapabilityProfileId"/>) lets one
/// engine register, say, two differently configured memory profiles — one
/// backed by an in-memory store for a test agent and one backed by a
/// durable store for a production agent — under the same capability without
/// either agent's definition needing to know the other profile exists.
/// </para>
/// </remarks>
public readonly record struct CapabilityProfileId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CapabilityProfileId"/>
    /// struct, validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty canonical profile identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public CapabilityProfileId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical profile identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical identifier text.</summary>
    public override string ToString() => Value;
}
