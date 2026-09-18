// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Portable JSON mirror of <see cref="ExecutionIdentity"/>, the complete authenticated subject and tenant captured at a trusted ingress.</summary>
/// <remarks>
/// <para>
/// Identity is authentication evidence and never grants authority, so this document is stored as evidence and revalidated on
/// read. Raw credentials must never enter it; only the safe <see cref="JsonAuthenticationEvidence"/> projection is persisted.
/// <see cref="AgentKit.TenantId"/>, <see cref="AgentKit.PrincipalId"/>, and <see cref="IdentityVersion"/> are unwrapped to
/// their underlying primitives and rebuilt through their validating constructors.
/// </para>
/// <para>
/// <see cref="Claims"/> and <see cref="DelegationChain"/> are ordered and their order is part of the evidence. Neither is ever
/// written as a default array, and both are reconstructed as real <see cref="ImmutableArray{T}"/> values, because the domain
/// constructor rejects default arrays. The domain additionally enforces that every delegation ancestor stays inside
/// <see cref="TenantId"/> and that <see cref="Assurance"/> never exceeds an ancestor's, so a tampered chain fails closed on
/// read rather than producing an identity that quietly claims more than it inherited.
/// </para>
/// </remarks>
/// <param name="TenantId">The non-blank tenant isolation boundary text.</param>
/// <param name="PrincipalId">The non-blank normalized principal text.</param>
/// <param name="SubjectKind">The normalized subject kind.</param>
/// <param name="Evidence">The non-null safe authentication evidence captured by the trusted ingress.</param>
/// <param name="Claims">The ordered, non-default, null-free normalized claims with issuer provenance.</param>
/// <param name="DelegationChain">The ordered, non-default, null-free same-tenant delegation ancestry.</param>
/// <param name="Assurance">The normalized authentication assurance, which must not exceed any ancestor link's assurance.</param>
/// <param name="Version">The positive version of issuer mapping and normalization used.</param>
public sealed record JsonExecutionIdentity(
    string TenantId,
    string PrincipalId,
    ExecutionSubjectKind SubjectKind,
    JsonAuthenticationEvidence Evidence,
    ImmutableArray<JsonIdentityClaim> Claims,
    ImmutableArray<JsonDelegationIdentityLink> DelegationChain,
    IdentityAssuranceLevel Assurance,
    long Version)
{
    /// <summary>Projects one domain execution identity into its portable JSON representation.</summary>
    /// <param name="value">The non-null identity to project.</param>
    /// <returns>A document carrying the unwrapped tenant, principal, and version plus never-default ordered claim and delegation arrays.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonExecutionIdentity FromDomain(ExecutionIdentity value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ImmutableArray<JsonIdentityClaim> claims =
            value.Claims.IsDefault ? [] : [.. value.Claims.Select(JsonIdentityClaim.FromDomain)];
        ImmutableArray<JsonDelegationIdentityLink> delegationChain = value.DelegationChain.IsDefault
            ? []
            : [.. value.DelegationChain.Select(JsonDelegationIdentityLink.FromDomain)];
        return new JsonExecutionIdentity(
            value.TenantId.Value,
            value.PrincipalId.Value,
            value.SubjectKind,
            JsonAuthenticationEvidence.FromDomain(value.Evidence),
            claims,
            delegationChain,
            value.Assurance,
            value.Version.Value);
    }

    /// <summary>Reconstructs the exact domain identity this document was projected from.</summary>
    /// <returns>An identity equal to the projected original, including ordered claim and delegation contents.</returns>
    /// <remarks>
    /// Default persisted arrays are normalized to empty arrays before reconstruction, and null elements are rejected instead
    /// of dereferenced. Every wrapper value is rebuilt through its own validating constructor before
    /// <see cref="ExecutionIdentity"/> revalidates tenant containment of the delegation chain and the assurance ceiling, so
    /// storage never becomes a path for widening an identity that the trusted ingress originally narrowed.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><see cref="Evidence"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentException">A persisted textual identity is blank, an array contains a null element, or a delegation ancestor names a different tenant.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="SubjectKind"/> or <see cref="Assurance"/> is outside its defined range, <see cref="Version"/> is not positive, or this identity's assurance exceeds an ancestor link's assurance.</exception>
    public ExecutionIdentity ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Evidence);
        var persistedClaims = Claims.IsDefault ? [] : Claims;
        ArgumentException.ThrowIfContainsNull(persistedClaims, nameof(Claims));
        var persistedChain = DelegationChain.IsDefault ? [] : DelegationChain;
        ArgumentException.ThrowIfContainsNull(persistedChain, nameof(DelegationChain));
        ImmutableArray<IdentityClaim> claims = [.. persistedClaims.Select(static claim => claim.ToDomain())];
        ImmutableArray<DelegationIdentityLink> delegationChain =
            [.. persistedChain.Select(static link => link.ToDomain())];
        return new ExecutionIdentity(
            new TenantId(TenantId),
            new PrincipalId(PrincipalId),
            SubjectKind,
            Evidence.ToDomain(),
            claims,
            delegationChain,
            Assurance,
            new IdentityVersion(Version));
    }

    /// <summary>Compares identities by ordered claim and delegation contents rather than by immutable-array storage identity.</summary>
    /// <param name="other">The candidate identity to compare with this one, which may be null.</param>
    /// <returns><see langword="true"/> when every scalar member and the projected evidence are equal and both ordered sequences are element-wise equal.</returns>
    /// <remarks>
    /// Compiler-generated record equality would compare the two arrays by backing-array identity, so two documents decoded
    /// from byte-identical JSON would compare unequal. This override restores the same ordered content equality the mirrored
    /// <see cref="ExecutionIdentity"/> guarantees.
    /// </remarks>
    public bool Equals(JsonExecutionIdentity? other) =>
        other is not null
        && TenantId == other.TenantId
        && PrincipalId == other.PrincipalId
        && SubjectKind == other.SubjectKind
        && Evidence == other.Evidence
        && Claims.AsSpan().SequenceEqual(other.Claims.AsSpan())
        && DelegationChain.AsSpan().SequenceEqual(other.DelegationChain.AsSpan())
        && Assurance == other.Assurance
        && Version == other.Version;

    /// <summary>Computes a hash consistent with <see cref="Equals(JsonExecutionIdentity?)"/>.</summary>
    /// <returns>A hash derived from every scalar member, the projected evidence, and each ordered claim and delegation link.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TenantId);
        hash.Add(PrincipalId);
        hash.Add(SubjectKind);
        hash.Add(Evidence);
        foreach (var claim in Claims.AsSpan())
        {
            hash.Add(claim);
        }

        foreach (var link in DelegationChain.AsSpan())
        {
            hash.Add(link);
        }

        hash.Add(Assurance);
        hash.Add(Version);
        return hash.ToHashCode();
    }
}
