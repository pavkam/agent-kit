// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies a JSON Schema dialect understood by an engine profile.</summary>
public readonly record struct OutputSchemaDialectId
{
    /// <summary>Initializes a dialect identity.</summary>
    /// <param name="value">The non-empty stable dialect identity text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public OutputSchemaDialectId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable dialect identity text.</summary>
    public string Value { get; }

    /// <summary>Returns the stable dialect identity text.</summary>
    public override string ToString() => Value;
}
