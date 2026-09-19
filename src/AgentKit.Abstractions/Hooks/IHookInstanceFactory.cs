// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Creates one <see cref="IHookActivationLease"/> for a captured hook catalog.</summary>
/// <remarks>
/// This is a typed DI activation boundary created by <c>AgentKit.Hooks</c>; runtime components depend on this
/// contract and the lease it returns, never on <see cref="IServiceProvider"/> directly. Each call creates one new
/// lease with its own scoped hook instances and reentrancy tracker; the caller owns the returned lease and must
/// dispose it exactly once at the end of its declared engine, agent, run, turn, or operation scope.
/// </remarks>
public interface IHookInstanceFactory
{
    /// <summary>Creates one activation lease for the given catalog.</summary>
    /// <param name="catalog">The captured catalog the lease's hook instances and tracker are scoped to.</param>
    /// <param name="cancellationToken">Cancels lease creation.</param>
    /// <returns>A newly created lease, owned by the caller.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is null.</exception>
    public ValueTask<IHookActivationLease> CreateAsync(
        HookCatalogSnapshot catalog,
        CancellationToken cancellationToken);
}
