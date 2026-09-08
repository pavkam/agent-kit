// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Immutable evidence of narrowly bounded authority whose remaining uses live in a grant store.</summary>
public sealed record SecurityGrant
{
    /// <summary>Initializes a bounded security grant.</summary>
    /// <param name="id">The grant identity.</param>
    /// <param name="requestId">The request that caused the grant.</param>
    /// <param name="scope">The exact execution scope.</param>
    /// <param name="identity">The authenticated execution identity.</param>
    /// <param name="audience">The only component permitted to consume the grant.</param>
    /// <param name="kind">The protected operation kind.</param>
    /// <param name="effect">The exact material effect.</param>
    /// <param name="resources">The ordered canonical resources.</param>
    /// <param name="inputFingerprint">The exact normalized input fingerprint.</param>
    /// <param name="policyVersion">The policy version that issued the grant.</param>
    /// <param name="revocationVersion">The revocation epoch captured at issue time.</param>
    /// <param name="notBefore">The earliest valid instant.</param>
    /// <param name="expiresAt">The exclusive expiry instant.</param>
    /// <param name="allowedUses">The positive maximum successful consumptions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/>, <paramref name="identity"/>, or <paramref name="resources"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="resources"/> is empty or <paramref name="expiresAt"/> is not later than <paramref name="notBefore"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum is undefined or <paramref name="allowedUses"/> is not positive.</exception>
    public SecurityGrant(
        GrantId id,
        SecurityRequestId requestId,
        SecurityAuthorizationScope scope,
        ExecutionIdentity identity,
        ComponentId audience,
        SecurityOperationKind kind,
        SecurityEffect effect,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint inputFingerprint,
        SecurityPolicyVersion policyVersion,
        SecurityRevocationVersion revocationVersion,
        DateTimeOffset notBefore,
        DateTimeOffset expiresAt,
        int allowedUses)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(effect);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(allowedUses);
        ArgumentException.ThrowIfDefaultOrEmpty(resources);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, notBefore);

        Id = id;
        RequestId = requestId;
        Scope = scope;
        Identity = identity;
        Audience = audience;
        Kind = kind;
        Effect = effect;
        Resources = resources;
        InputFingerprint = inputFingerprint;
        PolicyVersion = policyVersion;
        RevocationVersion = revocationVersion;
        NotBefore = notBefore;
        ExpiresAt = expiresAt;
        AllowedUses = allowedUses;
    }

    /// <summary>Initializes a grant that retains the complete captured authorization context evaluated for its request.</summary>
    /// <param name="id">The grant identity.</param><param name="requestId">The issuing request identity.</param><param name="scope">The exact execution scope.</param><param name="identity">The authenticated identity.</param><param name="authorization">The complete captured authorization evidence evaluated by the authority.</param><param name="audience">The sole consuming component.</param><param name="kind">The protected operation kind.</param><param name="effect">The exact material effect.</param><param name="resources">The ordered resources.</param><param name="inputFingerprint">The exact input fingerprint.</param><param name="policyVersion">The evaluated policy version.</param><param name="revocationVersion">The captured revocation epoch.</param><param name="notBefore">The first valid instant.</param><param name="expiresAt">The exclusive expiry.</param><param name="allowedUses">The positive use bound.</param>
    /// <exception cref="ArgumentNullException"><paramref name="authorization"/> is null.</exception><exception cref="ArgumentException">Its scope, identity, or policy-snapshot version differs from the grant.</exception>
    public SecurityGrant(GrantId id, SecurityRequestId requestId, SecurityAuthorizationScope scope,
        ExecutionIdentity identity, SecurityAuthorizationContext authorization, ComponentId audience,
        SecurityOperationKind kind, SecurityEffect effect, ImmutableArray<ProtectedResource> resources,
        InputFingerprint inputFingerprint, SecurityPolicyVersion policyVersion,
        SecurityRevocationVersion revocationVersion, DateTimeOffset notBefore, DateTimeOffset expiresAt,
        int allowedUses)
        : this(id, requestId, scope, identity, audience, kind, effect, resources, inputFingerprint,
            policyVersion, revocationVersion, notBefore, expiresAt, allowedUses)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfNotEqual(authorization.Scope, scope, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Identity, identity, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(
            authorization.PolicySnapshot.Version, policyVersion, nameof(authorization));
        Authorization = authorization;
    }

    /// <summary>Gets the grant identity.</summary>
    public GrantId Id { get; init; }
    /// <summary>Gets the originating request identity.</summary>
    public SecurityRequestId RequestId { get; init; }
    /// <summary>Gets or initializes the exact authorized scope.</summary>
    /// <value>The non-null scope, which must equal the captured authorization scope when <see cref="Authorization"/> is present.</value>
    /// <exception cref="ArgumentNullException">The initialized value is null.</exception>
    /// <exception cref="ArgumentException">A record copy assigns a scope different from the captured authorization scope.</exception>
    public SecurityAuthorizationScope Scope
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Scope));
            if (Authorization is not null)
            {
                ArgumentException.ThrowIfNotEqual(Authorization.Scope, value, nameof(Scope));
            }

            field = value;
        }
    }
    /// <summary>Gets or initializes the authenticated execution identity.</summary>
    /// <value>The non-null identity, which must equal the captured authorization identity when <see cref="Authorization"/> is present.</value>
    /// <exception cref="ArgumentNullException">The initialized value is null.</exception>
    /// <exception cref="ArgumentException">A record copy assigns an identity different from the captured authorization identity.</exception>
    public ExecutionIdentity Identity
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Identity));
            if (Authorization is not null)
            {
                ArgumentException.ThrowIfNotEqual(Authorization.Identity, value, nameof(Identity));
            }

            field = value;
        }
    }
    /// <summary>Gets the captured authorization context evaluated for this grant.</summary><value>The immutable captured selection, or null only for grants issued through the legacy unpinned path.</value>
    public SecurityAuthorizationContext? Authorization { get; }
    /// <summary>Gets the sole consuming component.</summary>
    public ComponentId Audience { get; init; }
    /// <summary>Gets the operation kind.</summary>
    public SecurityOperationKind Kind { get; init; }
    /// <summary>Gets the effect.</summary>
    public SecurityEffect Effect { get; init; }
    /// <summary>Gets the ordered canonical resources.</summary>
    public ImmutableArray<ProtectedResource> Resources { get; init; }
    /// <summary>Gets the normalized input fingerprint.</summary>
    public InputFingerprint InputFingerprint { get; init; }
    /// <summary>Gets or initializes the issuing policy version.</summary>
    /// <value>The issuing version, which must equal the captured policy-snapshot version when <see cref="Authorization"/> is present.</value>
    /// <exception cref="ArgumentException">A record copy assigns a version different from the captured policy snapshot.</exception>
    public SecurityPolicyVersion PolicyVersion
    {
        get;
        init
        {
            if (Authorization is not null)
            {
                ArgumentException.ThrowIfNotEqual(
                    Authorization.PolicySnapshot.Version, value, nameof(PolicyVersion));
            }

            field = value;
        }
    }
    /// <summary>Gets the captured revocation epoch.</summary>
    public SecurityRevocationVersion RevocationVersion { get; init; }
    /// <summary>Gets the earliest valid instant.</summary>
    public DateTimeOffset NotBefore { get; init; }
    /// <summary>Gets the exclusive expiry instant.</summary>
    public DateTimeOffset ExpiresAt { get; init; }
    /// <summary>Gets the maximum successful consumption count.</summary>
    public int AllowedUses { get; init; }

    /// <inheritdoc/>
    public bool Equals(SecurityGrant? other) =>
        other is not null
        && Id == other.Id
        && RequestId == other.RequestId
        && Scope == other.Scope
        && Identity == other.Identity
        && Authorization == other.Authorization
        && Audience == other.Audience
        && Kind == other.Kind
        && Effect == other.Effect
        && Resources.SequenceEqual(other.Resources)
        && InputFingerprint == other.InputFingerprint
        && PolicyVersion == other.PolicyVersion
        && RevocationVersion == other.RevocationVersion
        && NotBefore == other.NotBefore
        && ExpiresAt == other.ExpiresAt
        && AllowedUses == other.AllowedUses;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(RequestId);
        hash.Add(Scope);
        hash.Add(Identity);
        hash.Add(Authorization);
        hash.Add(Audience);
        hash.Add(Kind);
        hash.Add(Effect);
        foreach (var resource in Resources)
        {
            hash.Add(resource);
        }

        hash.Add(InputFingerprint);
        hash.Add(PolicyVersion);
        hash.Add(RevocationVersion);
        hash.Add(NotBefore);
        hash.Add(ExpiresAt);
        hash.Add(AllowedUses);
        return hash.ToHashCode();
    }
}
