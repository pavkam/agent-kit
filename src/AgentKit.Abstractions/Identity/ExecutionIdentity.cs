// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the authenticated subject and tenant captured at a trusted ingress for one operation.</summary>
/// <remarks>Identity is immutable authentication evidence and never grants authority. Raw credentials must never enter this value.</remarks>
public sealed record ExecutionIdentity
{
    /// <summary>Initializes an execution identity from evidence supplied by a trusted ingress.</summary>
    /// <param name="tenantId">The stable tenant isolation boundary.</param>
    /// <param name="principalId">The stable normalized principal.</param>
    /// <param name="subjectKind">The normalized subject kind.</param>
    /// <param name="evidence">The safe authentication evidence captured by trusted ingress.</param>
    /// <param name="claims">The ordered normalized claims with issuer provenance.</param>
    /// <param name="delegationChain">The ordered same-tenant delegation ancestry; ancestor principals may differ only when a host separately authenticated an impersonation transition.</param>
    /// <param name="assurance">The normalized authentication assurance.</param>
    /// <param name="version">The version of issuer mapping and normalization used.</param>
    /// <exception cref="ArgumentException">A nested identifier is default, either array is default or contains null, or a delegation link crosses the tenant boundary.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="evidence"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum value is undefined, the version is not positive, or this identity's assurance exceeds an ancestor link's assurance.</exception>
    public ExecutionIdentity(TenantId tenantId, PrincipalId principalId, ExecutionSubjectKind subjectKind, AuthenticationEvidence evidence, ImmutableArray<IdentityClaim> claims, ImmutableArray<DelegationIdentityLink> delegationChain, IdentityAssuranceLevel assurance, IdentityVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId.Value, nameof(principalId));
        ArgumentOutOfRangeException.ThrowIfNegative(
            (int) subjectKind, nameof(subjectKind));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            (int) subjectKind, (int) ExecutionSubjectKind.Anonymous, nameof(subjectKind));
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentException.ThrowIfContainsNull(claims);
        ArgumentException.ThrowIfCrossesTenant(delegationChain, tenantId);
        ArgumentOutOfRangeException.ThrowIfNegative(
            (int) assurance, nameof(assurance));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            (int) assurance, (int) IdentityAssuranceLevel.HardwareBacked, nameof(assurance));
        ArgumentOutOfRangeException.ThrowIfLessThan(version.Value, 1, nameof(version));
        foreach (var link in delegationChain)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                (int) assurance, (int) link.Assurance, nameof(assurance));
        }

        TenantId = tenantId;
        PrincipalId = principalId;
        SubjectKind = subjectKind;
        Evidence = evidence;
        Claims = claims;
        DelegationChain = delegationChain;
        Assurance = assurance;
        Version = version;
    }

    /// <summary>Gets the tenant isolation boundary.</summary>
    public TenantId TenantId { get; }
    /// <summary>Gets the normalized principal.</summary>
    public PrincipalId PrincipalId { get; }
    /// <summary>Gets the subject kind.</summary>
    public ExecutionSubjectKind SubjectKind { get; }
    /// <summary>Gets the safe evidence supplied by the trusted ingress.</summary>
    public AuthenticationEvidence Evidence { get; }
    /// <summary>Gets the ordered issuer-provenanced normalized claims.</summary>
    public ImmutableArray<IdentityClaim> Claims { get; }
    /// <summary>Gets the ordered delegation ancestry.</summary>
    public ImmutableArray<DelegationIdentityLink> DelegationChain { get; }
    /// <summary>Gets the normalized assurance.</summary>
    public IdentityAssuranceLevel Assurance { get; }
    /// <summary>Gets the captured mapping and normalization version.</summary>
    public IdentityVersion Version { get; }

    /// <summary>Compares identities using ordered claim and delegation contents rather than immutable-array storage identity.</summary>
    /// <param name="other">The identity to compare.</param>
    /// <returns><see langword="true"/> when every field and ordered collection matches.</returns>
    public bool Equals(ExecutionIdentity? other) => other is not null && TenantId == other.TenantId && PrincipalId == other.PrincipalId && SubjectKind == other.SubjectKind && Evidence == other.Evidence && Claims.AsSpan().SequenceEqual(other.Claims.AsSpan()) && DelegationChain.AsSpan().SequenceEqual(other.DelegationChain.AsSpan()) && Assurance == other.Assurance && Version == other.Version;

    /// <summary>Returns a content-based hash code consistent with <see cref="Equals(ExecutionIdentity?)"/>.</summary>
    /// <returns>A hash derived from every scalar and ordered collection element.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TenantId); hash.Add(PrincipalId); hash.Add(SubjectKind); hash.Add(Evidence);
        foreach (var claim in Claims)
        {
            hash.Add(claim);
        }
        foreach (var link in DelegationChain)
        {
            hash.Add(link);
        }
        hash.Add(Assurance); hash.Add(Version);
        return hash.ToHashCode();
    }
}
