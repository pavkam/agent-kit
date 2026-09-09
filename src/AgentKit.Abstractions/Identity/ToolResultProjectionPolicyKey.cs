// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one configured policy for projecting authoritative tool results into bounded message history.</summary>
/// <remarks>The value is an immutable ordinal composition key. Catalog publication and collision detection are runtime responsibilities.</remarks>
public readonly record struct ToolResultProjectionPolicyKey
{
    /// <summary>Initializes a projection-policy key with usable composition text.</summary>
    /// <param name="value">The nonblank, caller-selected policy key.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public ToolResultProjectionPolicyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the immutable policy-key text.</summary>
    /// <value>The nonblank ordinal key supplied at construction.</value>
    public string Value { get; }

    /// <summary>Returns the policy key text for composition diagnostics.</summary>
    /// <returns>The unchanged <see cref="Value"/> text.</returns>
    public override string ToString() => Value;
}
