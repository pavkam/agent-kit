// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One request to enter a hook point on the call path tracked by one <see cref="IHookActivationLease"/>.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization. <see cref="IHookInvocationTracker.TryEnter"/> uses it to decide whether the requested
/// dispatch may proceed against the depth already recorded for <see cref="Point"/> on this lease.
/// </remarks>
public sealed record HookInvocationAttempt
{
    /// <summary>Initializes a new instance of the <see cref="HookInvocationAttempt"/> record.</summary>
    /// <param name="point">The hook point being entered.</param>
    /// <param name="dispatchId">The dispatch this attempt belongs to.</param>
    /// <param name="reentrancy">The reentrancy policy declared by the dispatch's closed point definition.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="point"/> or <paramref name="dispatchId"/> is default, or <paramref name="reentrancy"/> is
    /// not a defined value.
    /// </exception>
    public HookInvocationAttempt(HookPointId point, HookDispatchId dispatchId, HookReentrancyPolicy reentrancy)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(point, default);
        ArgumentOutOfRangeException.ThrowIfEqual(dispatchId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(reentrancy);

        Point = point;
        DispatchId = dispatchId;
        Reentrancy = reentrancy;
    }

    /// <summary>Gets the hook point being entered.</summary>
    public HookPointId Point { get; }

    /// <summary>Gets the dispatch this attempt belongs to.</summary>
    public HookDispatchId DispatchId { get; }

    /// <summary>Gets the reentrancy policy declared by the dispatch's closed point definition.</summary>
    public HookReentrancyPolicy Reentrancy { get; }
}
