// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the trusted issuer whose mapping produced identity evidence or a claim.</summary>
public readonly record struct IdentityIssuerId
{
    /// <summary>Initializes a canonical issuer identifier.</summary>
    /// <param name="value">The non-blank, host-configured issuer key.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty, or whitespace.</exception>
    public IdentityIssuerId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical issuer key.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical issuer key.</summary>
    public override string ToString() => Value;
}
