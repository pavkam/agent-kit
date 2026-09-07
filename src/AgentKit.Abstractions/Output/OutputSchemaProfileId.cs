// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one immutable output-schema engine profile.</summary>
public readonly record struct OutputSchemaProfileId
{
    /// <summary>Initializes a profile identity.</summary>
    /// <param name="value">The non-empty stable identity text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public OutputSchemaProfileId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable profile identity text.</summary>
    public string Value { get; }

    /// <summary>Returns the stable profile identity text.</summary>
    public override string ToString() => Value;
}
