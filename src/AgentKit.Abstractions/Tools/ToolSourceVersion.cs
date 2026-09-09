// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one immutable publication from a tool source.</summary>
/// <remarks>Source-version text is source-defined, compared ordinally, and does not imply global ordering.</remarks>
public readonly record struct ToolSourceVersion
{
    /// <summary>Initializes a tool-source publication version.</summary>
    /// <param name="value">The nonblank source-defined version text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public ToolSourceVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the source-defined version text.</summary>
    /// <value>Ordinal text retained without normalization.</value>
    public string Value { get; }

    /// <summary>Returns the source-defined version text.</summary>
    /// <returns>The value supplied at construction.</returns>
    public override string ToString() => Value;
}
