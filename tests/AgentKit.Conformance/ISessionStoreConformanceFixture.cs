// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Composes one isolated protected session store and issues exact single-use authority for conformance operations.</summary>
/// <remarks>
/// Each inherited case owns and disposes one fixture. Every authorization call must create a fresh grant and enforcement
/// intent for the complete immutable request, register that grant with the store's actual enforcement dependency, and
/// allow the store to produce the authoritative intent receipt when it consumes the grant.
/// </remarks>
public interface ISessionStoreConformanceFixture: IAsyncDisposable
{
    /// <summary>Creates the protected store through the implementation's public composition surface.</summary>
    /// <param name="cancellationToken">Cancels before composition completes.</param>
    /// <returns>The public store contract used by the conformance case.</returns>
    public ValueTask<ISessionStore> CreateAsync(CancellationToken cancellationToken = default);

    /// <summary>Issues fresh exact authority for one request against the store created by <see cref="CreateAsync"/>.</summary>
    /// <typeparam name="TRequest">The immutable session request type.</typeparam>
    /// <param name="request">The complete request whose canonical resource and content fingerprint must be bound.</param>
    /// <param name="kind">The exact protected operation kind enforced by the target store method.</param>
    /// <param name="effect">The exact protected effect enforced by the target store method.</param>
    /// <param name="cancellationToken">Cancels before authority is issued or registered.</param>
    /// <returns>A request wrapper carrying a newly registered single-use grant and a fresh enforcement intent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><see cref="CreateAsync"/> has not composed a store.</exception>
    public ValueTask<AuthorizedSessionStoreRequest<TRequest>> AuthorizeAsync<TRequest>(
        TRequest request,
        SecurityOperationKind kind,
        SecurityEffect effect,
        CancellationToken cancellationToken = default)
        where TRequest : class;
}
