// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects one named budget policy evaluated during scope admission or reservation.</summary>
/// <remarks>This immutable value uses ordinal text equality and carries selection identity only; it does not reserve or grant budget.</remarks>
public readonly record struct BudgetPolicyKey
{
    /// <summary>Initializes a validated budget-policy selection key.</summary>
    /// <param name="value">The non-blank canonical policy key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or consists only of whitespace.</exception>
    public BudgetPolicyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical policy key text.</summary>
    /// <value>The exact caller-supplied text. A default instance exposes <see langword="null"/> at runtime.</value>
    public string Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;
}
