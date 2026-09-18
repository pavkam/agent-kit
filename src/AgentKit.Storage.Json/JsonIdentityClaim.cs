// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Portable JSON mirror of <see cref="IdentityClaim"/>, one normalized and issuer-provenanced claim safe for policy evaluation.</summary>
/// <remarks>
/// <para>
/// A claim's issuer provenance is what makes it usable in a policy decision, so <see cref="Issuer"/> is persisted alongside
/// the claim rather than inferred from the enclosing identity. The domain wrapper <see cref="IdentityIssuerId"/> is unwrapped
/// to its canonical key text here and rebuilt through its validating constructor on read.
/// </para>
/// <para>
/// <see cref="ValueKind"/> is a domain enum and is persisted directly, because it describes how the already-normalized
/// <see cref="Value"/> text is interpreted. The value itself stays text for every kind, exactly as the domain models it, so
/// booleans and whole numbers keep the canonical encoding the issuer normalization produced rather than being re-encoded by a
/// JSON writer.
/// </para>
/// </remarks>
/// <param name="Issuer">The non-blank canonical key of the trusted issuer that vouched for the claim.</param>
/// <param name="Type">The non-blank normalized claim type.</param>
/// <param name="Value">The non-blank normalized claim value, encoded as text for every <paramref name="ValueKind"/>.</param>
/// <param name="ValueKind">The portable interpretation of <paramref name="Value"/>.</param>
public sealed record JsonIdentityClaim(
    string Issuer,
    string Type,
    string Value,
    IdentityClaimValueKind ValueKind)
{
    /// <summary>Projects one domain claim into its portable JSON representation.</summary>
    /// <param name="value">The non-null claim to project.</param>
    /// <returns>A document carrying the unwrapped issuer key, claim type, claim value, and value kind.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonIdentityClaim FromDomain(IdentityClaim value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonIdentityClaim(value.Issuer.Value, value.Type, value.Value, value.ValueKind);
    }

    /// <summary>Reconstructs the exact domain claim this document was projected from.</summary>
    /// <returns>A claim equal to the projected original.</returns>
    /// <remarks>
    /// The issuer is rebuilt through <see cref="IdentityIssuerId(string)"/> and the claim through
    /// <see cref="IdentityClaim(IdentityIssuerId, string, string, IdentityClaimValueKind)"/>, so a persisted blank issuer,
    /// type, or value, or a value kind outside the defined range, is rejected as invalid evidence rather than admitted into a
    /// policy decision.
    /// </remarks>
    /// <exception cref="ArgumentException">The persisted issuer, type, or value is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ValueKind"/> is outside the defined <see cref="IdentityClaimValueKind"/> range.</exception>
    public IdentityClaim ToDomain() => new IdentityClaim(new IdentityIssuerId(Issuer), Type, Value, ValueKind);
}
