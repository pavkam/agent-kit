// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The unit of measure a <see cref="BudgetDimension"/> is expressed in, such
/// as <c>"tokens"</c>, <c>"calls"</c>, <c>"usd"</c>, or <c>"seconds"</c>.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization. A <see cref="BudgetDimensionDescriptor"/> declares the
/// legal units for its dimension so a reservation expressed in an
/// incompatible unit fails before it can be misinterpreted.
/// </remarks>
public readonly record struct BudgetUnit
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetUnit"/> struct,
    /// validating that it carries usable unit text.
    /// </summary>
    /// <param name="value">The non-empty canonical unit text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public BudgetUnit(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical unit text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical unit text, suitable for logging and diagnostic
    /// messages.
    /// </summary>
    public override string ToString() => Value;
}
