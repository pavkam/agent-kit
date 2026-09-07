// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures safe, immutable evidence that a trusted ingress authenticated a subject.</summary>
public sealed record AuthenticationEvidence
{
    /// <summary>Initializes authentication evidence without retaining credentials.</summary>
    /// <param name="id">The durable safe evidence reference.</param>
    /// <param name="issuer">The issuer that established the evidence.</param>
    /// <param name="method">The non-blank normalized authentication method.</param>
    /// <param name="authenticatedAt">The instant authentication completed.</param>
    /// <param name="expiresAt">The optional instant after which the evidence is invalid.</param>
    /// <param name="safeFingerprint">A one-way safe fingerprint suitable for correlation.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/>, <paramref name="issuer"/>, or <paramref name="safeFingerprint"/> is default, or <paramref name="method"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expiresAt"/> is not later than <paramref name="authenticatedAt"/>.</exception>
    public AuthenticationEvidence(AuthenticationEvidenceId id, IdentityIssuerId issuer, string method, DateTimeOffset authenticatedAt, DateTimeOffset? expiresAt, AuthenticationEvidenceFingerprint safeFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Value, nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer.Value, nameof(issuer));
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeFingerprint.Hash.Value, nameof(safeFingerprint));
        if (expiresAt is { } expiry)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiry, authenticatedAt, nameof(expiresAt));
        }

        Id = id;
        Issuer = issuer;
        Method = method;
        AuthenticatedAt = authenticatedAt;
        ExpiresAt = expiresAt;
        SafeFingerprint = safeFingerprint;
    }

    /// <summary>Gets the safe evidence reference.</summary>
    public AuthenticationEvidenceId Id { get; }
    /// <summary>Gets the issuer that authenticated the subject.</summary>
    public IdentityIssuerId Issuer { get; }
    /// <summary>Gets the normalized authentication method.</summary>
    public string Method { get; }
    /// <summary>Gets the authentication instant.</summary>
    public DateTimeOffset AuthenticatedAt { get; }
    /// <summary>Gets the optional expiry instant.</summary>
    public DateTimeOffset? ExpiresAt { get; }
    /// <summary>Gets the safe evidence fingerprint.</summary>
    public AuthenticationEvidenceFingerprint SafeFingerprint { get; }
}
