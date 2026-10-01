// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Performs one already-authorized child handoff.</summary>
/// <remarks>
/// A local dispatcher commits an idempotent child-admission intent and returns its handoff receipt; it never captures the
/// engine, a runner, or a callback that would create a constructor cycle through the loop. A remote dispatcher follows the
/// same durable handoff contract. Every effecting dispatcher validates its own delegation grant.
/// </remarks>
public interface IDelegationDispatcher
{
    /// <summary>Gets the dispatcher's audience and lifetime claims.</summary>
    /// <value>An immutable descriptor that does not change after construction.</value>
    public DelegationDispatcherDescriptor Descriptor { get; }

    /// <summary>Hands one authorized child off for execution.</summary>
    /// <param name="request">The authorized delegation.</param>
    /// <param name="cancellationToken">Cancels the wait; it does not withdraw a handoff that already committed.</param>
    /// <returns>The handoff receipt or a typed rejection. Replaying the same delegation returns the same receipt and creates no second child.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the handoff commits.</exception>
    public Task<DelegationResult> DispatchAsync(AuthorizedDelegation request, CancellationToken cancellationToken = default);
}
