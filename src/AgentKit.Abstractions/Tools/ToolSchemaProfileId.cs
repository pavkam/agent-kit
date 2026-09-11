// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one immutable tool-schema engine policy independently of a tool or provider identity.</summary>
/// <remarks>Ordinal identity names a profile family; its separate version pins keyword and resource-accounting semantics.</remarks>
public readonly record struct ToolSchemaProfileId
{
    /// <summary>Creates an exact profile identity without normalizing its spelling.</summary>
    /// <param name="value">Nonblank profile identity text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public ToolSchemaProfileId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }
    /// <summary>Gets the exact profile family name.</summary>
    /// <value>Nonblank text on a constructed value; default values are invalid at consuming boundaries.</value>
    public string Value { get; }
    /// <summary>Returns the exact profile family name for diagnostics or serialization.</summary>
    /// <returns>The retained identity text.</returns>
    public override string ToString() => Value;
}
