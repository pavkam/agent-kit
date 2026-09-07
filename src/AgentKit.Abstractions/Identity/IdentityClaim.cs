// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents one normalized, issuer-provenanced identity claim safe for policy evaluation.</summary>
public sealed record IdentityClaim
{
    /// <summary>Initializes a normalized claim.</summary>
    /// <param name="issuer">The trusted issuer that vouched for the claim.</param>
    /// <param name="type">The non-blank normalized claim type.</param>
    /// <param name="value">The non-blank normalized claim value.</param>
    /// <param name="valueKind">The portable interpretation of <paramref name="value"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="type"/> or <paramref name="value"/> is null, empty, or whitespace.</exception>
    public IdentityClaim(IdentityIssuerId issuer, string type, string value, IdentityClaimValueKind valueKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer.Value, nameof(issuer));
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfNegative(
            (int) valueKind, nameof(valueKind));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            (int) valueKind, (int) IdentityClaimValueKind.WholeNumber, nameof(valueKind));
        Issuer = issuer;
        Type = type;
        Value = value;
        ValueKind = valueKind;
    }

    /// <summary>Gets the issuer that established the claim.</summary>
    public IdentityIssuerId Issuer { get; }
    /// <summary>Gets the normalized claim type.</summary>
    public string Type { get; }
    /// <summary>Gets the normalized bounded value.</summary>
    public string Value { get; }
    /// <summary>Gets the value interpretation.</summary>
    public IdentityClaimValueKind ValueKind { get; }
}
