// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one addressable runtime component instance, independent of
/// the keyed contract selection (see <see cref="ComponentKey{TContract}"/>)
/// used to resolve which implementation type backs it.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// A <see cref="ComponentKey{TContract}"/> answers "which registered
/// implementation should be used for this contract?" — a compile-time,
/// type-scoped selection. A <see cref="ComponentId"/> instead names one
/// concrete, addressable instance of a component after it has been
/// resolved, for use in diagnostics, tracing, and observability where a
/// human or a monitoring system needs to say "component X reported this
/// event" without caring which contract or key produced it.
/// </para>
/// </remarks>
public readonly record struct ComponentId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentId"/> struct,
    /// validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty canonical component identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ComponentId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical component identifier text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical identifier text, suitable for logging and
    /// diagnostic messages.
    /// </summary>
    public override string ToString() => Value;
}
