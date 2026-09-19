// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one registration's individual execution within one dispatch.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization. One <see cref="AgentHookEventArgs"/> instance is shared across every hook in a
/// dispatch, so it cannot truthfully expose one invocation identity; the dispatcher creates a fresh instance of
/// this type for each hook it invokes and supplies it alongside the shared event arguments. Diagnostics,
/// reentrancy tracking, and paired unwind bookkeeping all correlate through <see cref="InvocationId"/>.
/// </remarks>
public sealed record HookInvocationContext
{
    /// <summary>Initializes a new instance of the <see cref="HookInvocationContext"/> record.</summary>
    /// <param name="registrationId">The registration being invoked.</param>
    /// <param name="invocationId">This individual execution's stable identity.</param>
    /// <param name="dispatchId">The dispatch this invocation belongs to.</param>
    /// <param name="depth">The reentrant dispatch depth this invocation observed for its hook point on entry.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="registrationId"/>, <paramref name="invocationId"/>, or <paramref name="dispatchId"/> is
    /// default, or <paramref name="depth"/> is negative.
    /// </exception>
    public HookInvocationContext(
        HookRegistrationId registrationId,
        HookInvocationId invocationId,
        HookDispatchId dispatchId,
        int depth)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(registrationId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(invocationId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(dispatchId, default);
        ArgumentOutOfRangeException.ThrowIfNegative(depth);

        RegistrationId = registrationId;
        InvocationId = invocationId;
        DispatchId = dispatchId;
        Depth = depth;
    }

    /// <summary>Gets the registration being invoked.</summary>
    public HookRegistrationId RegistrationId { get; }

    /// <summary>Gets this individual execution's stable identity.</summary>
    public HookInvocationId InvocationId { get; }

    /// <summary>Gets the dispatch this invocation belongs to.</summary>
    public HookDispatchId DispatchId { get; }

    /// <summary>Gets the reentrant dispatch depth this invocation observed for its hook point on entry.</summary>
    public int Depth { get; }
}
