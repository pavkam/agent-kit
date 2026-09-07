// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records one immutable delegation ancestor and the identity constraints established at that step.</summary>
public sealed record DelegationIdentityLink
{
    /// <summary>Initializes a delegation-chain link.</summary>
    /// <param name="id">The delegation operation identity.</param>
    /// <param name="tenantId">The tenant preserved from the parent.</param>
    /// <param name="principalId">The parent principal retained as causal provenance, which may differ only through a separately authenticated host impersonation mechanism.</param>
    /// <param name="issuer">The trusted issuer captured by the parent identity.</param>
    /// <param name="evidenceId">The parent's safe evidence reference.</param>
    /// <param name="version">The parent's issuer-mapping and normalization version.</param>
    /// <param name="delegatedAt">The instant the trusted derivation occurred.</param>
    /// <param name="claims">The narrowed normalized claims retained by the child.</param>
    /// <param name="assurance">The child's assurance, which must not exceed its parent.</param>
    /// <exception cref="ArgumentException">A textual identity is default or <paramref name="claims"/> is default or contains null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default, <paramref name="version"/> is not positive, or <paramref name="assurance"/> is undefined.</exception>
    public DelegationIdentityLink(DelegationId id, TenantId tenantId, PrincipalId principalId, IdentityIssuerId issuer, AuthenticationEvidenceId evidenceId, IdentityVersion version, DateTimeOffset delegatedAt, ImmutableArray<IdentityClaim> claims, IdentityAssuranceLevel assurance)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id.Value, Guid.Empty, nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId.Value, nameof(principalId));
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer.Value, nameof(issuer));
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceId.Value, nameof(evidenceId));
        ArgumentOutOfRangeException.ThrowIfLessThan(version.Value, 1, nameof(version));
        ArgumentException.ThrowIfContainsNull(claims);
        ArgumentOutOfRangeException.ThrowIfNegative(
            (int) assurance, nameof(assurance));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            (int) assurance, (int) IdentityAssuranceLevel.HardwareBacked, nameof(assurance));
        Id = id;
        TenantId = tenantId;
        PrincipalId = principalId;
        Issuer = issuer;
        EvidenceId = evidenceId;
        Version = version;
        DelegatedAt = delegatedAt;
        Claims = claims;
        Assurance = assurance;
    }

    /// <summary>Gets the delegation identity.</summary>
    public DelegationId Id { get; }
    /// <summary>Gets the retained tenant.</summary>
    public TenantId TenantId { get; }
    /// <summary>Gets the causal principal at this delegation step.</summary>
    public PrincipalId PrincipalId { get; }
    /// <summary>Gets the parent identity's trusted issuer.</summary>
    public IdentityIssuerId Issuer { get; }
    /// <summary>Gets the parent identity's safe evidence reference.</summary>
    public AuthenticationEvidenceId EvidenceId { get; }
    /// <summary>Gets the parent identity's mapping and normalization version.</summary>
    public IdentityVersion Version { get; }
    /// <summary>Gets the derivation instant.</summary>
    public DateTimeOffset DelegatedAt { get; }
    /// <summary>Gets the claims retained at this step.</summary>
    public ImmutableArray<IdentityClaim> Claims { get; }
    /// <summary>Gets the assurance retained at this step.</summary>
    public IdentityAssuranceLevel Assurance { get; }

    /// <summary>Compares links using ordered claim contents.</summary>
    /// <param name="other">The link to compare.</param>
    /// <returns><see langword="true"/> when every field and ordered claim matches.</returns>
    public bool Equals(DelegationIdentityLink? other) => other is not null && Id == other.Id && TenantId == other.TenantId && PrincipalId == other.PrincipalId && Issuer == other.Issuer && EvidenceId == other.EvidenceId && Version == other.Version && DelegatedAt == other.DelegatedAt && Claims.AsSpan().SequenceEqual(other.Claims.AsSpan()) && Assurance == other.Assurance;

    /// <summary>Returns a content-based hash code.</summary>
    /// <returns>A hash derived from every field and ordered claim.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id); hash.Add(TenantId); hash.Add(PrincipalId); hash.Add(Issuer); hash.Add(EvidenceId); hash.Add(Version); hash.Add(DelegatedAt);
        foreach (var claim in Claims)
        {
            hash.Add(claim);
        }
        hash.Add(Assurance);
        return hash.ToHashCode();
    }
}
