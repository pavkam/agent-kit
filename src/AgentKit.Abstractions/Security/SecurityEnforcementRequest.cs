// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Fresh concrete effect evidence checked immediately before consuming a grant.</summary>
public sealed record SecurityEnforcementRequest
{
    /// <summary>Initializes exact enforcement evidence.</summary>
    /// <param name="scope">The actual execution scope.</param>
    /// <param name="identity">The actual execution identity.</param>
    /// <param name="audience">The effecting component.</param>
    /// <param name="kind">The concrete operation kind.</param>
    /// <param name="effect">The concrete effect.</param>
    /// <param name="resources">The concrete ordered canonical resources.</param>
    /// <param name="inputFingerprint">The concrete normalized input fingerprint.</param>
    /// <param name="revocationVersion">The current revocation epoch.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> or <paramref name="identity"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="resources"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum is undefined.</exception>
    public SecurityEnforcementRequest(
        SecurityAuthorizationScope scope,
        ExecutionIdentity identity,
        ComponentId audience,
        SecurityOperationKind kind,
        SecurityEffect effect,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint inputFingerprint,
        SecurityRevocationVersion revocationVersion)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(effect);
        ArgumentException.ThrowIfDefaultOrEmpty(resources);

        Scope = scope;
        Identity = identity;
        Audience = audience;
        Kind = kind;
        Effect = effect;
        Resources = resources;
        InputFingerprint = inputFingerprint;
        RevocationVersion = revocationVersion;
    }

    /// <summary>Initializes concrete enforcement evidence retaining the complete captured authorization context.</summary>
    /// <param name="scope">The actual execution scope.</param><param name="identity">The actual identity.</param><param name="authorization">The complete captured authorization evidence presented by the protected operation.</param><param name="audience">The effecting component.</param><param name="kind">The concrete operation kind.</param><param name="effect">The concrete effect.</param><param name="resources">The concrete ordered resources.</param><param name="inputFingerprint">The concrete input fingerprint.</param><param name="revocationVersion">The current revocation epoch.</param>
    /// <exception cref="ArgumentNullException"><paramref name="authorization"/> is null.</exception><exception cref="ArgumentException">Its scope or identity differs from the enforcement request.</exception>
    public SecurityEnforcementRequest(SecurityAuthorizationScope scope, ExecutionIdentity identity,
        SecurityAuthorizationContext authorization, ComponentId audience, SecurityOperationKind kind,
        SecurityEffect effect, ImmutableArray<ProtectedResource> resources, InputFingerprint inputFingerprint,
        SecurityRevocationVersion revocationVersion)
        : this(scope, identity, audience, kind, effect, resources, inputFingerprint, revocationVersion)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfNotEqual(authorization.Scope, scope, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Identity, identity, nameof(authorization));
        Authorization = authorization;
    }

    /// <summary>Gets or initializes the actual scope.</summary>
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
    /// <summary>Gets or initializes the actual identity.</summary>
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
    /// <summary>Gets complete captured authorization evidence for snapshot-bound enforcement.</summary><value>The immutable captured selection, or null only for legacy enforcement paths.</value>
    public SecurityAuthorizationContext? Authorization { get; }
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
}
