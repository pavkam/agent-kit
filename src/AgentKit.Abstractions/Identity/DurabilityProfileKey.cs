// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Selects one named durability profile, which binds a backend, operation
/// allowlist, checkpoint policy, lease policy, and recovery policy into a
/// single reusable configuration an agent definition can choose.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal, textual)
/// equality over <see cref="Value"/>. It carries no mutable state and is safe
/// to share and compare across threads without synchronization.
/// </para>
/// <para>
/// The profile key is the single place durability is turned on for an agent.
/// An agent definition that selects no profile is not durable, and that is a
/// valid composition rather than an error. A per-run override may request
/// less durability or tighter limits, but never a backend or retry mode the
/// selected profile excludes.
/// </para>
/// </remarks>
public readonly record struct DurabilityProfileKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DurabilityProfileKey"/>
    /// struct, validating that it carries usable key text.
    /// </summary>
    /// <param name="value">The non-empty canonical profile key.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public DurabilityProfileKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical profile key text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical profile key text, suitable for logging and
    /// composition-validation messages.
    /// </summary>
    public override string ToString() => Value;
}
