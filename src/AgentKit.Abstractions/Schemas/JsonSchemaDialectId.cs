// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one explicit JSON Schema dialect across schema-owning components.</summary>
/// <remarks>
/// Dialect identity is shared by tool, output, and other schema contracts. It
/// describes syntax and vocabulary selection; it does not prove that a
/// particular engine supports that dialect or any keyword within it.
/// </remarks>
public readonly record struct JsonSchemaDialectId
{
    /// <summary>Initializes a dialect identity.</summary>
    /// <param name="value">The non-empty stable dialect identity text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or whitespace.</exception>
    public JsonSchemaDialectId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable dialect identity text.</summary>
    /// <value>Ordinal text retained without normalization or implicit aliasing.</value>
    public string Value { get; }

    /// <summary>Returns the stable dialect identity text.</summary>
    /// <returns>The exact text supplied at construction.</returns>
    public override string ToString() => Value;
}
