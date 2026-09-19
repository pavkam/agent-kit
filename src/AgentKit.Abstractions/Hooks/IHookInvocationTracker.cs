// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Tracks in-memory reentrant dispatch depth for one <see cref="IHookActivationLease"/>, without using ambient or async-local state.</summary>
/// <remarks>
/// A tracker instance belongs to exactly one activation lease and is disposed with it. It synchronously guards
/// only in-memory depth, cycle, and reentrancy state; the hook work it brackets remains asynchronous and
/// cancellable. Implementations must be safe to call concurrently, since sibling tool calls or other concurrently
/// dispatched points may share one lease.
/// </remarks>
public interface IHookInvocationTracker
{
    /// <summary>Attempts to enter a hook point, honoring its declared reentrancy policy and the host's depth ceiling.</summary>
    /// <param name="attempt">The hook point, dispatch, and reentrancy policy being attempted.</param>
    /// <returns>
    /// A <see cref="HookInvocationTrackingEntered"/> result carrying the newly active depth, or a
    /// <see cref="HookInvocationTrackingRejected"/> result when entering would exceed the permitted depth.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="attempt"/> is null.</exception>
    public HookInvocationTrackingResult TryEnter(HookInvocationAttempt attempt);

    /// <summary>Records that one previously entered hook point dispatch has completed.</summary>
    /// <param name="dispatchId">The dispatch identity supplied to the matching successful <see cref="TryEnter"/> call.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dispatchId"/> is default.</exception>
    /// <remarks>Every successful <see cref="TryEnter"/> must be matched by exactly one <see cref="Leave"/> call, even when the dispatch fails.</remarks>
    public void Leave(HookDispatchId dispatchId);
}
