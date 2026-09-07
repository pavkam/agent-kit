// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An extensible, validated key identifying one measurable or enforceable
/// axis of consumption, such as model requests, output tokens, or wall-clock
/// run duration.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share, compare, and use as a lookup key across
/// threads without synchronization.
/// </para>
/// <para>
/// This is deliberately not a closed enumeration: AgentKit reserves the
/// <c>agentkit.*</c> namespace for the first-party dimensions declared on
/// <see cref="BudgetDimensions"/>, and third-party dimensions use their own
/// stable namespace and register a <see cref="BudgetDimensionDescriptor"/>
/// declaring their aggregation semantics and legal units. An unregistered
/// dimension fails composition rather than becoming a counter with guessed
/// semantics.
/// </para>
/// </remarks>
public readonly record struct BudgetDimension
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetDimension"/>
    /// struct, validating that it carries usable dimension text.
    /// </summary>
    /// <param name="value">The non-empty canonical dimension key text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public BudgetDimension(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical dimension key text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical dimension key text, suitable for logging and
    /// diagnostic messages.
    /// </summary>
    public override string ToString() => Value;
}
