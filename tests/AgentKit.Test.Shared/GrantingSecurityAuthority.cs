// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>A deterministic security authority that records every request and issues single-use grants into a store.</summary>
/// <remarks>
/// The authority allows every request unless a denial predicate matches. Each allowed request becomes a grant bound to
/// exactly the request's audience, resources, and fingerprint, registered with the supplied
/// <see cref="ConsumingGrantStore"/> so the effecting boundary's own consumption is checked against it.
/// </remarks>
public sealed class GrantingSecurityAuthority: ISecurityAuthority
{
    private readonly Lock _gate = new();
    private readonly ConsumingGrantStore _store;
    private readonly List<SecurityRequest> _requests = [];
    private int _sequence;

    /// <summary>Initializes an authority that registers its grants with <paramref name="store"/>.</summary>
    /// <param name="store">The store that later consumes the grants.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is null.</exception>
    public GrantingSecurityAuthority(ConsumingGrantStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>Gets or sets a predicate selecting requests that are denied instead of granted.</summary>
    /// <value>The predicate, or <see langword="null"/> to allow every request.</value>
    public Func<SecurityRequest, bool>? Deny { get; set; }

    /// <summary>Gets or sets an exception thrown from every authorization, simulating an unavailable authority.</summary>
    /// <value>The exception to throw, or <see langword="null"/> to decide normally.</value>
    public Exception? Fault { get; set; }

    /// <summary>Gets a snapshot of every security request received, in order.</summary>
    public IReadOnlyList<SecurityRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityDecision> AuthorizeAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        int sequence;
        lock (_gate)
        {
            _requests.Add(request);
            sequence = ++_sequence;
        }

        if (Fault is { } fault)
        {
            throw fault;
        }

        if (Deny?.Invoke(request) is true)
        {
            return new SecurityDenied(
                request.Id,
                new SecurityPolicyVersion(1),
                new SecurityDenial("test.denied", "Denied."));
        }

        var grant = new SecurityGrant(
            new GrantId(Guid.Parse($"10000000-0000-0000-0000-{sequence:D12}")),
            request.Id,
            request.Scope,
            request.Identity,
            request.Authorization,
            request.Audience,
            request.Kind,
            request.Effect,
            request.Resources,
            request.InputFingerprint,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            request.Deadline,
            1);
        await _store.RegisterAsync(grant, cancellationToken).ConfigureAwait(false);
        return new SecurityAllowed(request.Id, new SecurityPolicyVersion(1), grant);
    }
}
