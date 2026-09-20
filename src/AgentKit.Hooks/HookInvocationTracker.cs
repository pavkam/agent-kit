// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>
/// Tracks in-memory reentrant dispatch depth for one activation lease, keyed by hook point.
/// </summary>
/// <remarks>
/// <para>
/// One tracker belongs to exactly one <see cref="IHookActivationLease"/> and must not be shared across leases.
/// Calls are safe to make concurrently: sibling dispatches on the same lease take one lock, and no async-local
/// or other ambient depth is consulted. The lock covers only the depth maps. Hook work itself stays outside
/// the tracker and remains asynchronous and cancellable.
/// </para>
/// <para>
/// Depth is counted per <see cref="HookPointId"/>. <see cref="HookReentrancyPolicy.Forbidden"/> permits one
/// active dispatch of a point (permitted depth 1) regardless of the configured ceiling.
/// <see cref="HookReentrancyPolicy.Bounded"/> permits nested dispatch up to the ceiling supplied at construction,
/// which is the host <see cref="AgentHookOptions.MaximumInvocationDepth"/> once the activation lease owns the
/// tracker. A rejected attempt does not change recorded depth.
/// </para>
/// </remarks>
public sealed class HookInvocationTracker: IHookInvocationTracker
{
    private readonly int _maximumInvocationDepth;
    private readonly Lock _gate = new();
    private readonly Dictionary<HookPointId, int> _depth = [];
    private readonly Dictionary<HookDispatchId, HookPointId> _active = [];

    /// <summary>Initializes a new instance of the <see cref="HookInvocationTracker"/> class.</summary>
    /// <param name="maximumInvocationDepth">
    /// The host ceiling on simultaneous active dispatches of one hook point. Bounded reentrancy cannot exceed it.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumInvocationDepth"/> is less than 1.</exception>
    public HookInvocationTracker(int maximumInvocationDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumInvocationDepth);
        _maximumInvocationDepth = maximumInvocationDepth;
    }

    /// <summary>Attempts to enter a hook point, honoring its declared reentrancy policy and the host's depth ceiling.</summary>
    /// <param name="attempt">The hook point, dispatch, and reentrancy policy being attempted.</param>
    /// <returns>
    /// A <see cref="HookInvocationTrackingEntered"/> result carrying the newly active depth, or a
    /// <see cref="HookInvocationTrackingRejected"/> result when entering would exceed the permitted depth.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="attempt"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="attempt"/> reuses a <see cref="HookDispatchId"/> that this tracker already entered and has not left.
    /// </exception>
    public HookInvocationTrackingResult TryEnter(HookInvocationAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        lock (_gate)
        {
            if (_active.ContainsKey(attempt.DispatchId))
            {
                throw new InvalidOperationException(
                    $"Hook dispatch '{attempt.DispatchId}' is already active on this invocation tracker.");
            }

            var permittedDepth = attempt.Reentrancy == HookReentrancyPolicy.Bounded
                ? _maximumInvocationDepth
                : 1;
            var activeDepth = _depth.GetValueOrDefault(attempt.Point);
            if (activeDepth >= permittedDepth)
            {
                return new HookInvocationTrackingRejected(activeDepth, permittedDepth);
            }

            var depth = activeDepth + 1;
            _depth[attempt.Point] = depth;
            _active.Add(attempt.DispatchId, attempt.Point);
            return new HookInvocationTrackingEntered(depth);
        }
    }

    /// <summary>Records that one previously entered hook point dispatch has completed.</summary>
    /// <param name="dispatchId">The dispatch identity supplied to the matching successful <see cref="TryEnter"/> call.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dispatchId"/> is default.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="dispatchId"/> was not returned by a successful <see cref="TryEnter"/>, or it was already left.
    /// </exception>
    /// <remarks>Every successful <see cref="TryEnter"/> must be matched by exactly one <see cref="Leave"/> call, even when the dispatch fails.</remarks>
    public void Leave(HookDispatchId dispatchId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(dispatchId, default);

        lock (_gate)
        {
            if (!_active.Remove(dispatchId, out var point))
            {
                throw new InvalidOperationException(
                    $"Hook dispatch '{dispatchId}' is not active on this invocation tracker.");
            }

            if (!_depth.TryGetValue(point, out var depth) || depth <= 0)
            {
                throw new InvalidOperationException(
                    $"Hook dispatch '{dispatchId}' was active without recorded depth for point '{point}'.");
            }

            if (depth == 1)
            {
                _ = _depth.Remove(point);
                return;
            }

            _depth[point] = depth - 1;
        }
    }
}
