// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Portable JSON mirror of <see cref="DelegationIdentityLink"/>, one immutable delegation ancestor and the identity constraints established at that step.</summary>
/// <remarks>
/// <para>
/// A delegation link is the evidence that lets a child identity prove what it inherited and what it narrowed. Every wrapper
/// value it holds — <see cref="DelegationId"/>, <see cref="AgentKit.TenantId"/>, <see cref="AgentKit.PrincipalId"/>,
/// <see cref="IdentityIssuerId"/>, <see cref="AuthenticationEvidenceId"/>, and <see cref="IdentityVersion"/> — is unwrapped to
/// its underlying primitive here and rebuilt through its own validating constructor on read, so the persisted form contains no
/// domain types a JSON serializer would have to be taught about.
/// </para>
/// <para>
/// <see cref="Claims"/> is ordered and its order is part of the evidence, because the mirrored domain type compares links by
/// ordered claim contents. <see cref="FromDomain"/> never produces a default array, and <see cref="ToDomain"/> always produces
/// a real <see cref="ImmutableArray{T}"/>, so the default-versus-empty distinction cannot leak through storage into a domain
/// constructor that rejects default arrays outright.
/// </para>
/// </remarks>
/// <param name="Id">The raw value of the non-empty delegation operation identity.</param>
/// <param name="TenantId">The non-blank tenant text preserved from the parent identity.</param>
/// <param name="PrincipalId">The non-blank parent principal text retained as causal provenance.</param>
/// <param name="Issuer">The non-blank canonical key of the trusted issuer captured by the parent identity.</param>
/// <param name="EvidenceId">The non-blank safe authentication-evidence reference of the parent identity.</param>
/// <param name="Version">The positive issuer-mapping and normalization version captured from the parent.</param>
/// <param name="DelegatedAt">The instant the trusted derivation occurred.</param>
/// <param name="Claims">The ordered, non-default, null-free narrowed claims retained by the child at this step.</param>
/// <param name="Assurance">The assurance retained at this step, which must not exceed the parent's.</param>
public sealed record JsonDelegationIdentityLink(
    Guid Id,
    string TenantId,
    string PrincipalId,
    string Issuer,
    string EvidenceId,
    long Version,
    DateTimeOffset DelegatedAt,
    ImmutableArray<JsonIdentityClaim> Claims,
    IdentityAssuranceLevel Assurance)
{
    /// <summary>Projects one domain delegation link into its portable JSON representation.</summary>
    /// <param name="value">The non-null link to project.</param>
    /// <returns>A document carrying the unwrapped identities and an ordered, never-default claim array.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonDelegationIdentityLink FromDomain(DelegationIdentityLink value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ImmutableArray<JsonIdentityClaim> claims =
            value.Claims.IsDefault ? [] : [.. value.Claims.Select(JsonIdentityClaim.FromDomain)];
        return new JsonDelegationIdentityLink(
            value.Id.Value,
            value.TenantId.Value,
            value.PrincipalId.Value,
            value.Issuer.Value,
            value.EvidenceId.Value,
            value.Version.Value,
            value.DelegatedAt,
            claims,
            value.Assurance);
    }

    /// <summary>Reconstructs the exact domain delegation link this document was projected from.</summary>
    /// <returns>A link equal to the projected original, including ordered claim contents.</returns>
    /// <remarks>
    /// A persisted default claim array is normalized to an empty array before reconstruction, because the domain constructor
    /// rejects default arrays and an absent JSON array is indistinguishable from an empty one. Null claim elements are
    /// rejected instead of dereferenced, and every wrapper value is rebuilt through its own validating constructor before
    /// <see cref="DelegationIdentityLink"/> revalidates the combination.
    /// </remarks>
    /// <exception cref="ArgumentException">A persisted textual identity is blank, or <see cref="Claims"/> contains a null element or an invalid claim.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The persisted delegation identity is empty, <see cref="Version"/> is not positive, or <see cref="Assurance"/> is outside the defined range.</exception>
    public DelegationIdentityLink ToDomain()
    {
        var persisted = Claims.IsDefault ? [] : Claims;
        ArgumentException.ThrowIfContainsNull(persisted, nameof(Claims));
        ImmutableArray<IdentityClaim> claims = [.. persisted.Select(static claim => claim.ToDomain())];
        return new DelegationIdentityLink(
            new DelegationId(Id),
            new TenantId(TenantId),
            new PrincipalId(PrincipalId),
            new IdentityIssuerId(Issuer),
            new AuthenticationEvidenceId(EvidenceId),
            new IdentityVersion(Version),
            DelegatedAt,
            claims,
            Assurance);
    }

    /// <summary>Compares links by ordered claim contents rather than by immutable-array storage identity.</summary>
    /// <param name="other">The candidate link to compare with this one, which may be null.</param>
    /// <returns><see langword="true"/> when every scalar member is equal and both claim sequences are element-wise equal in the same order.</returns>
    /// <remarks>
    /// Compiler-generated record equality would compare <see cref="Claims"/> by the identity of its backing array, so two
    /// documents decoded from byte-identical JSON would compare unequal. This override restores the same ordered content
    /// equality the mirrored <see cref="DelegationIdentityLink"/> guarantees, which round-trip assertions and store-level
    /// deduplication depend on.
    /// </remarks>
    public bool Equals(JsonDelegationIdentityLink? other) =>
        other is not null
        && Id == other.Id
        && TenantId == other.TenantId
        && PrincipalId == other.PrincipalId
        && Issuer == other.Issuer
        && EvidenceId == other.EvidenceId
        && Version == other.Version
        && DelegatedAt == other.DelegatedAt
        && Claims.AsSpan().SequenceEqual(other.Claims.AsSpan())
        && Assurance == other.Assurance;

    /// <summary>Computes a hash consistent with <see cref="Equals(JsonDelegationIdentityLink?)"/>.</summary>
    /// <returns>A hash derived from every scalar member and each ordered claim.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(TenantId);
        hash.Add(PrincipalId);
        hash.Add(Issuer);
        hash.Add(EvidenceId);
        hash.Add(Version);
        hash.Add(DelegatedAt);
        foreach (var claim in Claims.AsSpan())
        {
            hash.Add(claim);
        }

        hash.Add(Assurance);
        return hash.ToHashCode();
    }
}
