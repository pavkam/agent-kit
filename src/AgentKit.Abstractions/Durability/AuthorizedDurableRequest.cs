// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Pairs one exact durable journal request with its selected journal and single-use grant.</summary>
/// <typeparam name="TRequest">The immutable request shape enforced by the journal.</typeparam>
/// <remarks>The journal recomputes concrete enforcement evidence immediately before access. A wrapper cannot authorize another journal, request, identity, or retry, and its grant is consumed at most once.</remarks>
public sealed record AuthorizedDurableRequest<TRequest>
    where TRequest : class
{
    /// <summary>Initializes a protected durable journal request.</summary>
    /// <param name="request">The non-null immutable operation request.</param>
    /// <param name="journalKey">The exact selected journal registration.</param>
    /// <param name="grant">The non-null single-use grant issued for this exact access.</param>
    /// <param name="intent">The non-null stable consumption attempt and required ownership fence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/>, <paramref name="grant"/>, its captured authorization, or <paramref name="intent"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="journalKey"/> is blank.</exception>
    public AuthorizedDurableRequest(
        TRequest request,
        DurableJournalKey journalKey,
        SecurityGrant grant,
        SecurityEnforcementIntent intent)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(journalKey.Value, nameof(journalKey));
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization);
        ArgumentNullException.ThrowIfNull(intent);
        Request = request;
        JournalKey = journalKey;
        Grant = grant;
        Intent = intent;
    }

    /// <summary>Gets the immutable request.</summary><value>The exact request whose effect must be recomputed.</value>
    public TRequest Request { get; }

    /// <summary>Gets the selected journal key.</summary><value>The nonblank journal identity the effect must target.</value>
    public DurableJournalKey JournalKey { get; }

    /// <summary>Gets the presented grant.</summary><value>Immutable authority evidence whose use remains owned by the grant store.</value>
    public SecurityGrant Grant { get; }

    /// <summary>Gets the stable grant-consumption intent.</summary><value>The non-null attempt identity and required fence that the grant store must persist atomically with consumption.</value>
    public SecurityEnforcementIntent Intent { get; }
}
