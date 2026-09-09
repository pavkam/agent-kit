// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the explicit source that published a tool descriptor.</summary>
/// <remarks>The source identifies registration provenance and never grants authority to invoke its tools.</remarks>
public readonly record struct ToolSourceId
{
    /// <summary>Initializes a tool-source identity.</summary>
    /// <param name="value">The nonblank stable source text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public ToolSourceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable source identity text.</summary>
    /// <value>Ordinal text retained without normalization.</value>
    public string Value { get; }

    /// <summary>Returns the stable source identity text.</summary>
    /// <returns>The value supplied at construction.</returns>
    public override string ToString() => Value;
}
