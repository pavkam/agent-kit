// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents one bounded claim supplied by a trusted adapter before issuer-specific normalization.</summary>
public sealed record ExternalIdentityClaim
{
    /// <summary>Initializes an external claim.</summary>
    /// <param name="type">The non-blank issuer-defined claim type.</param>
    /// <param name="value">The non-blank issuer-defined claim value.</param>
    /// <param name="valueKind">The portable representation used by the adapter.</param>
    /// <exception cref="ArgumentException"><paramref name="type"/> or <paramref name="value"/> is null, empty, or whitespace.</exception>
    public ExternalIdentityClaim(string type, string value, IdentityClaimValueKind valueKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfNegative(
            (int) valueKind, nameof(valueKind));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            (int) valueKind, (int) IdentityClaimValueKind.WholeNumber, nameof(valueKind));
        Type = type;
        Value = value;
        ValueKind = valueKind;
    }

    /// <summary>Gets the issuer-defined claim type.</summary>
    public string Type { get; }
    /// <summary>Gets the bounded claim value.</summary>
    public string Value { get; }
    /// <summary>Gets the value representation.</summary>
    public IdentityClaimValueKind ValueKind { get; }
}
