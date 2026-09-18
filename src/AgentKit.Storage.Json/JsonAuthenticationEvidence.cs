// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Portable JSON mirror of <see cref="AuthenticationEvidence"/>, the safe record that a trusted ingress authenticated a subject.</summary>
/// <remarks>
/// <para>
/// This document contains no credential material, and it must never be extended to carry any. It mirrors exactly what the
/// domain value holds: a safe evidence reference, the issuer that vouched for it, the normalized method name, the
/// authentication and optional expiry instants, and a one-way fingerprint. The fingerprint is unwrapped from
/// <see cref="AuthenticationEvidenceFingerprint"/> through <see cref="ContentHash"/> to its underlying text, and rebuilt
/// through both constructors on read.
/// </para>
/// <para>
/// <see cref="ExpiresAt"/> is preserved as null when the evidence does not expire. The domain constructor requires a present
/// expiry to be strictly later than <see cref="AuthenticatedAt"/>, so a document whose clock evidence was corrupted is
/// rejected on read rather than resurrected as already-expired identity evidence.
/// </para>
/// </remarks>
/// <param name="Id">The non-blank safe evidence reference that names the authentication record without containing it.</param>
/// <param name="Issuer">The non-blank canonical key of the trusted issuer that established the evidence.</param>
/// <param name="Method">The non-blank normalized authentication method name.</param>
/// <param name="AuthenticatedAt">The instant authentication completed.</param>
/// <param name="ExpiresAt">The instant after which the evidence is invalid, which must be strictly later than <paramref name="AuthenticatedAt"/>, or <see langword="null"/> when the evidence does not expire.</param>
/// <param name="SafeFingerprint">The non-blank one-way fingerprint text used for correlation and cache partitioning; it must not encode a raw credential.</param>
public sealed record JsonAuthenticationEvidence(
    string Id,
    string Issuer,
    string Method,
    DateTimeOffset AuthenticatedAt,
    DateTimeOffset? ExpiresAt,
    string SafeFingerprint)
{
    /// <summary>Projects one domain authentication evidence value into its portable JSON representation.</summary>
    /// <param name="value">The non-null evidence to project.</param>
    /// <returns>A document carrying the unwrapped reference, issuer, method, instants, and fingerprint text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonAuthenticationEvidence FromDomain(AuthenticationEvidence value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonAuthenticationEvidence(
            value.Id.Value,
            value.Issuer.Value,
            value.Method,
            value.AuthenticatedAt,
            value.ExpiresAt,
            value.SafeFingerprint.Hash.Value);
    }

    /// <summary>Reconstructs the exact domain evidence this document was projected from.</summary>
    /// <returns>Evidence equal to the projected original, including a preserved null expiry.</returns>
    /// <remarks>
    /// The reference, issuer, content hash, and fingerprint wrappers are each rebuilt through their own validating
    /// constructors before <see cref="AuthenticationEvidence"/> revalidates the combination, so blank persisted text fails
    /// closed instead of producing a default-valued identity that later policy code would treat as present evidence.
    /// </remarks>
    /// <exception cref="ArgumentException">A persisted reference, issuer, method, or fingerprint is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ExpiresAt"/> is present and is not later than <see cref="AuthenticatedAt"/>.</exception>
    public AuthenticationEvidence ToDomain()
    {
        return new AuthenticationEvidence(
            new AuthenticationEvidenceId(Id),
            new IdentityIssuerId(Issuer),
            Method,
            AuthenticatedAt,
            ExpiresAt,
            new AuthenticationEvidenceFingerprint(new ContentHash(SafeFingerprint)));
    }
}
