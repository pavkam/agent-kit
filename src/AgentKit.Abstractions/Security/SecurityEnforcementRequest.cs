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

    /// <summary>Gets the actual scope.</summary>
    public SecurityAuthorizationScope Scope { get; init; }
    /// <summary>Gets the actual identity.</summary>
    public ExecutionIdentity Identity { get; init; }
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
