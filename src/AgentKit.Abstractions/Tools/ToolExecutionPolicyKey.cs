// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names a retained tool-execution policy family.</summary>
/// <remarks>The key selects policy evidence only when paired with an exact positive version.</remarks>
public readonly record struct ToolExecutionPolicyKey
{
    /// <summary>Initializes a tool-execution policy key.</summary>
    /// <param name="value">The nonblank stable policy key text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public ToolExecutionPolicyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the stable policy-key text.</summary>
    /// <value>Ordinal text retained without normalization.</value>
    public string Value { get; }

    /// <summary>Returns the stable policy-key text.</summary>
    /// <returns>The value supplied at construction.</returns>
    public override string ToString() => Value;
}
