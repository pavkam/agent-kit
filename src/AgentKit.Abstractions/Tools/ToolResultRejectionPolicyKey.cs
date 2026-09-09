// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names a retained run-level tool-result rejection policy.</summary>
public readonly record struct ToolResultRejectionPolicyKey
{
    /// <summary>Creates an ordinal key without normalization or implicit policy lookup.</summary>
    /// <param name="value">The nonblank stable policy-family text.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public ToolResultRejectionPolicyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }
    /// <summary>Gets the stable policy key.</summary>
    /// <value>Nonblank ordinal text, or null only on the CLR default value.</value>
    public string? Value { get; }

    /// <summary>Formats the exact key without culture-sensitive conversion.</summary>
    /// <returns>The original key text, or empty text for the CLR default value.</returns>
    public override string ToString() => Value ?? string.Empty;
}
