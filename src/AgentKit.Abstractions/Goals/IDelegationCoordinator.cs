// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Authorizes, records intent for, and dispatches delegated child goals, and decides joins over them.</summary>
/// <remarks>
/// <para>
/// The coordinator validates the parent, selects the target, evaluates the policy pipeline, reserves budget, asks the
/// security authority for a bounded delegation grant, durably creates the child goal, and hands it to the dispatcher the
/// captured profile names. It fails closed before any child exists when authority, target, policy, budget, or required
/// audit is unavailable.
/// </para>
/// <para>
/// The live caller supplies <see cref="HookDispatchContext"/> separately; it is never persisted in a request, goal, or
/// result. Delayed dispatch with no active run passes <see langword="null"/> rather than reconstructing a hook tracker.
/// </para>
/// </remarks>
public interface IDelegationCoordinator
{
    /// <summary>Delegates one bounded objective and waits until the child settles, the request's deadline passes, or the wait is cancelled.</summary>
    /// <param name="request">The canonical delegation request.</param>
    /// <param name="hooks">The live hook context of the calling run, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the caller's wait. It is distinct from durable abort: an already-dispatched child keeps its declared cancellation relationship.</param>
    /// <returns>A rejection before child creation, or a child result whose status is terminal or <see cref="DelegationStatus.Dispatched"/> when the deadline passed first. Replaying the same idempotency key returns the one existing child.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public Task<DelegationResult> DelegateAsync(
        DelegationRequest request,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken = default);

    /// <summary>Decides a parent's join over its durable children, optionally waiting for a pending decision.</summary>
    /// <param name="request">The join request.</param>
    /// <param name="hooks">The live hook context of the calling run, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A pending, satisfied, or unsatisfiable decision.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<GoalJoinDecision> JoinAsync(
        GoalJoinRequest request,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken = default);
}
