// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Fresh concrete effect evidence checked immediately before consuming a grant.</summary>
public sealed record SecurityEnforcementRequest
{
    /// <summary>Initializes concrete enforcement evidence retaining the complete captured authorization context.</summary>
    /// <param name="scope">The actual execution scope.</param>
    /// <param name="identity">The actual execution identity.</param>
    /// <param name="authorization">The complete captured authorization evidence presented by the protected operation.</param>
    /// <param name="audience">The effecting component.</param>
    /// <param name="kind">The concrete operation kind.</param>
    /// <param name="effect">The concrete effect.</param>
    /// <param name="resources">The concrete ordered canonical resources.</param>
    /// <param name="inputFingerprint">The concrete normalized input fingerprint.</param>
    /// <param name="revocationVersion">The current revocation epoch.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/>, <paramref name="identity"/>, or <paramref name="authorization"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="resources"/> is empty, or the authorization's scope or identity differs from the enforcement request.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum is undefined.</exception>
    public SecurityEnforcementRequest(
        SecurityAuthorizationScope scope,
        ExecutionIdentity identity,
        SecurityAuthorizationContext authorization,
        ComponentId audience,
        SecurityOperationKind kind,
        SecurityEffect effect,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint inputFingerprint,
        SecurityRevocationVersion revocationVersion)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(effect);
        ArgumentException.ThrowIfDefaultOrEmpty(resources);
        ArgumentException.ThrowIfNotEqual(authorization.Scope, scope, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Identity, identity, nameof(authorization));

        Authorization = authorization;
        Scope = scope;
        Identity = identity;
        Audience = audience;
        Kind = kind;
        Effect = effect;
        Resources = resources;
        InputFingerprint = inputFingerprint;
        RevocationVersion = revocationVersion;
    }

    /// <summary>Gets or initializes the actual scope.</summary>
    /// <value>The non-null scope, which must equal the captured authorization scope.</value>
    /// <exception cref="ArgumentNullException">The initialized value is null.</exception>
    /// <exception cref="ArgumentException">A record copy assigns a scope different from the captured authorization scope.</exception>
    public SecurityAuthorizationScope Scope
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Scope));
            ArgumentException.ThrowIfNotEqual(Authorization.Scope, value, nameof(Scope));
            field = value;
        }
    }
    /// <summary>Gets or initializes the actual identity.</summary>
    /// <value>The non-null identity, which must equal the captured authorization identity.</value>
    /// <exception cref="ArgumentNullException">The initialized value is null.</exception>
    /// <exception cref="ArgumentException">A record copy assigns an identity different from the captured authorization identity.</exception>
    public ExecutionIdentity Identity
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Identity));
            ArgumentException.ThrowIfNotEqual(Authorization.Identity, value, nameof(Identity));
            field = value;
        }
    }
    /// <summary>Gets the complete captured authorization evidence the protected operation presents.</summary><value>The immutable captured selection; never null.</value>
    public SecurityAuthorizationContext Authorization { get; }
    /// <summary>Gets the effecting component.</summary>
    public ComponentId Audience { get; init; }
    /// <summary>Gets the actual operation kind.</summary>
    public SecurityOperationKind Kind { get; init; }
    /// <summary>Gets the actual effect.</summary>
    public SecurityEffect Effect { get; init; }
    /// <summary>Gets the actual ordered resources.</summary>
    public ImmutableArray<ProtectedResource> Resources { get; init; }
    /// <summary>Gets the actual input fingerprint.</summary>
    public InputFingerprint InputFingerprint { get; init; }
    /// <summary>Gets the current revocation epoch.</summary>
    public SecurityRevocationVersion RevocationVersion { get; init; }

    /// <summary>Compares complete enforcement evidence using ordered structural resource equality.</summary>
    /// <param name="other">The candidate enforcement evidence.</param>
    /// <returns><see langword="true"/> only when every scalar, captured context, and ordered resource is equal.</returns>
    public bool Equals(SecurityEnforcementRequest? other) =>
        other is not null
        && Scope == other.Scope
        && Identity == other.Identity
        && Authorization == other.Authorization
        && Audience == other.Audience
        && Kind == other.Kind
        && Effect == other.Effect
        && ResourcesEqual(Resources, other.Resources)
        && InputFingerprint == other.InputFingerprint
        && RevocationVersion == other.RevocationVersion;

    /// <summary>Computes a hash from every scalar, captured context, and ordered resource.</summary>
    /// <returns>A value consistent with structural enforcement equality.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Scope);
        hash.Add(Identity);
        hash.Add(Authorization);
        hash.Add(Audience);
        hash.Add(Kind);
        hash.Add(Effect);
        if (!Resources.IsDefault)
        {
            foreach (var resource in Resources)
            {
                hash.Add(resource);
            }
        }
        hash.Add(InputFingerprint);
        hash.Add(RevocationVersion);
        return hash.ToHashCode();
    }

    private static bool ResourcesEqual(
        ImmutableArray<ProtectedResource> left,
        ImmutableArray<ProtectedResource> right) => left.IsDefault || right.IsDefault
        ? left.IsDefault == right.IsDefault
        : left.AsSpan().SequenceEqual(right.AsSpan());
}
