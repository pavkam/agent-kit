// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Pairs one exact session-store request with its selected store and single-use grant.</summary>
/// <typeparam name="TRequest">The immutable request shape enforced by the store.</typeparam>
/// <remarks>The store recomputes concrete enforcement evidence immediately before access. A wrapper cannot authorize another store, request, identity, or retry, and its grant is consumed at most once.</remarks>
public sealed record AuthorizedSessionStoreRequest<TRequest>
    where TRequest : class
{
    /// <summary>Initializes a protected session-store request.</summary>
    /// <param name="request">The non-null immutable operation request.</param>
    /// <param name="storeKey">The exact selected store registration.</param>
    /// <param name="grant">The non-null single-use grant issued for this exact access.</param>
    /// <param name="intent">The non-null stable consumption attempt and required ownership fence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/>, <paramref name="grant"/>, its captured authorization, or <paramref name="intent"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="storeKey"/> is blank.</exception>
    public AuthorizedSessionStoreRequest(TRequest request, SessionStoreKey storeKey, SecurityGrant grant,
        SecurityEnforcementIntent intent)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(storeKey.Value, nameof(storeKey));
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization);
        ArgumentNullException.ThrowIfNull(intent);
        Request = request;
        StoreKey = storeKey;
        Grant = grant;
        Intent = intent;
    }

    /// <summary>Gets the immutable request.</summary><value>The exact request whose effect must be recomputed.</value>
    public TRequest Request { get; }
    /// <summary>Gets the selected store key.</summary><value>The nonblank store identity the effect must target.</value>
    public SessionStoreKey StoreKey { get; }
    /// <summary>Gets the presented grant.</summary><value>Immutable authority evidence whose use remains owned by the grant store.</value>
    public SecurityGrant Grant { get; }
    /// <summary>Gets the stable grant-consumption intent.</summary><value>The non-null attempt identity and required fence that the grant store must persist atomically with consumption.</value>
    public SecurityEnforcementIntent Intent { get; }
}
