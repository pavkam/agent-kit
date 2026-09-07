// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests a same-tenant, same-principal child identity with claims and assurance narrowed from its parent.</summary>
public sealed record DelegatedIdentityRequest
{
    /// <summary>Initializes a delegation request that has no principal-switch fields by design.</summary>
    /// <param name="delegationId">The operation identity for this derivation.</param>
    /// <param name="parent">The immutable parent identity.</param>
    /// <param name="claims">The ordered subset of parent claims requested for the child.</param>
    /// <param name="maximumAssurance">The maximum assurance requested for the child.</param>
    /// <exception cref="ArgumentNullException"><paramref name="parent"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="claims"/> is default, contains null, or contains a claim absent from the parent.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumAssurance"/> exceeds the parent's assurance.</exception>
    public DelegatedIdentityRequest(DelegationId delegationId, ExecutionIdentity parent, ImmutableArray<IdentityClaim> claims, IdentityAssuranceLevel maximumAssurance)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(delegationId.Value, Guid.Empty, nameof(delegationId));
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfContainsNull(claims);
        ArgumentOutOfRangeException.ThrowIfNegative(
            (int) maximumAssurance, nameof(maximumAssurance));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            (int) maximumAssurance, (int) IdentityAssuranceLevel.HardwareBacked, nameof(maximumAssurance));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            (int) maximumAssurance, (int) parent.Assurance, nameof(maximumAssurance));
        ArgumentException.ThrowIfNotSubsetOf(claims, parent.Claims);

        DelegationId = delegationId;
        Parent = parent;
        Claims = claims;
        MaximumAssurance = maximumAssurance;
    }

    /// <summary>Gets the delegation operation identity.</summary>
    public DelegationId DelegationId { get; }
    /// <summary>Gets the identity being narrowed.</summary>
    public ExecutionIdentity Parent { get; }
    /// <summary>Gets the requested subset of issuer-provenanced claims.</summary>
    public ImmutableArray<IdentityClaim> Claims { get; }
    /// <summary>Gets the maximum child assurance.</summary>
    public IdentityAssuranceLevel MaximumAssurance { get; }

    /// <summary>Compares requests using ordered claim contents.</summary>
    /// <param name="other">The request to compare.</param>
    /// <returns><see langword="true"/> when all fields and ordered claims match.</returns>
    public bool Equals(DelegatedIdentityRequest? other) => other is not null && DelegationId == other.DelegationId && Parent == other.Parent && Claims.AsSpan().SequenceEqual(other.Claims.AsSpan()) && MaximumAssurance == other.MaximumAssurance;

    /// <summary>Returns a content-based hash code.</summary>
    /// <returns>A hash derived from the request and ordered claims.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode(); hash.Add(DelegationId); hash.Add(Parent);
        foreach (var claim in Claims)
        {
            hash.Add(claim);
        }
        hash.Add(MaximumAssurance); return hash.ToHashCode();
    }
}
