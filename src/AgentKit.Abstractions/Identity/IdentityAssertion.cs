// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries a trusted adapter's bounded assertion into identity normalization; it is not a wire DTO.</summary>
public sealed record IdentityAssertion
{
    /// <summary>Initializes a trusted identity assertion.</summary>
    /// <param name="issuer">The configured issuer that authenticated the subject.</param>
    /// <param name="externalSubject">The non-blank issuer-local subject.</param>
    /// <param name="claims">The bounded external claims to normalize.</param>
    /// <param name="evidence">The safe authentication evidence.</param>
    /// <exception cref="ArgumentException"><paramref name="issuer"/> is default, <paramref name="externalSubject"/> is blank, or <paramref name="claims"/> is default or contains null.</exception>
    /// <exception cref="ArgumentException"><paramref name="evidence"/> names a different issuer.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="evidence"/> is null.</exception>
    public IdentityAssertion(IdentityIssuerId issuer, string externalSubject, ImmutableArray<ExternalIdentityClaim> claims, AuthenticationEvidence evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer.Value, nameof(issuer));
        ArgumentException.ThrowIfNullOrWhiteSpace(externalSubject);
        ArgumentException.ThrowIfContainsNull(claims);
        ArgumentException.ThrowIfIssuerMismatch(evidence, issuer);

        Issuer = issuer;
        ExternalSubject = externalSubject;
        Claims = claims;
        Evidence = evidence;
    }

    /// <summary>Gets the trusted issuer key.</summary>
    public IdentityIssuerId Issuer { get; }
    /// <summary>Gets the issuer-local subject string.</summary>
    public string ExternalSubject { get; }
    /// <summary>Gets the bounded, unnormalized claims.</summary>
    public ImmutableArray<ExternalIdentityClaim> Claims { get; }
    /// <summary>Gets the safe authentication evidence.</summary>
    public AuthenticationEvidence Evidence { get; }

    /// <summary>Compares assertions using claim contents rather than immutable-array storage identity.</summary>
    /// <param name="other">The assertion to compare.</param>
    /// <returns><see langword="true"/> when all scalar values and ordered claims match.</returns>
    public bool Equals(IdentityAssertion? other) => other is not null
        && Issuer == other.Issuer
        && StringComparer.Ordinal.Equals(ExternalSubject, other.ExternalSubject)
        && Claims.AsSpan().SequenceEqual(other.Claims.AsSpan())
        && Evidence == other.Evidence;

    /// <summary>Returns a content-based hash code consistent with <see cref="Equals(IdentityAssertion?)"/>.</summary>
    /// <returns>A hash code derived from all assertion fields and ordered claims.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Issuer);
        hash.Add(ExternalSubject, StringComparer.Ordinal);
        foreach (var claim in Claims)
        {
            hash.Add(claim);
        }
        hash.Add(Evidence);
        return hash.ToHashCode();
    }
}
