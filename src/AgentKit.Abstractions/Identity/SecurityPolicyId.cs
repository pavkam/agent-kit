// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one declared security policy for safe selection and audit evidence.</summary>
/// <remarks>The identifier is immutable, non-empty host configuration; it does not contain request content or grant authority.</remarks>
public readonly record struct SecurityPolicyId
{
    /// <summary>Initializes one non-empty policy identifier.</summary>
    /// <param name="value">The canonical host-declared policy identifier.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public SecurityPolicyId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical host-declared policy identifier.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical identifier for safe diagnostics and audit facts.</summary>
    public override string ToString() => Value;
}
