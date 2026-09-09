// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the exact provider-visible name advertised for a tool.</summary>
/// <remarks>The alias is immutable request evidence. It does not imply that a canonical tool identity was resolved.</remarks>
public readonly record struct ToolAlias
{
    /// <summary>Initializes a provider-visible tool alias.</summary>
    /// <param name="value">The nonblank alias text advertised to the model.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public ToolAlias(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the exact provider-visible alias text.</summary>
    /// <value>Ordinal text retained without normalization.</value>
    public string Value { get; }

    /// <summary>Returns the exact provider-visible alias text.</summary>
    /// <returns>The value supplied at construction.</returns>
    public override string ToString() => Value;
}
