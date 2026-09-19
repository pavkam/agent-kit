// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns the scoped hook instances and reentrancy tracker for one captured <see cref="HookCatalogSnapshot"/>, at its declared engine, agent, run, turn, or operation scope.</summary>
/// <remarks>
/// A lease is created by <see cref="IHookInstanceFactory"/> and is a typed DI activation boundary: runtime
/// components resolve hook instances only through <see cref="ResolveAsync{THook}"/> and never receive or query an
/// <see cref="IServiceProvider"/>. The scope owner disposes the lease exactly once, which disposes every scoped or
/// transient hook instance it created and its <see cref="InvocationTracker"/>; singleton hook instances outlive
/// the lease and are not disposed by it.
/// </remarks>
public interface IHookActivationLease: IAsyncDisposable
{
    /// <summary>Gets the reentrancy tracker scoped to this lease.</summary>
    public IHookInvocationTracker InvocationTracker { get; }

    /// <summary>Resolves the hook instance for one registration.</summary>
    /// <typeparam name="THook">The closed hook interface for the registration's point.</typeparam>
    /// <param name="registrationId">The registration to resolve an instance for.</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>
    /// A <see cref="HookInstanceResolved{THook}"/> result carrying the instance, or a
    /// <see cref="HookInstanceUnavailable{THook}"/> result when the registration is unknown to this lease's
    /// captured catalog or its implementation cannot be activated.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="registrationId"/> is default.</exception>
    /// <exception cref="ObjectDisposedException">This lease has already been disposed.</exception>
    public ValueTask<HookInstanceResolution<THook>> ResolveAsync<THook>(
        HookRegistrationId registrationId,
        CancellationToken cancellationToken)
        where THook : class;
}
