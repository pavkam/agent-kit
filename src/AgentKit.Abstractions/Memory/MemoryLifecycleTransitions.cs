// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines which memory lifecycle transitions a store may apply.</summary>
/// <remarks>
/// The lifecycle is <c>Proposed → Validated → Accepted → Active</c>, with rejection available before activation and correction
/// or expiry available from active. Deletion is a separate store operation that is allowed from every non-deleted state.
/// Rejected, corrected, and expired records are terminal for transitions.
/// </remarks>
public static class MemoryLifecycleTransitions
{
    /// <summary>Determines whether a lifecycle transition is defined.</summary>
    /// <param name="from">The current state.</param>
    /// <param name="to">The requested state.</param>
    /// <returns><see langword="true"/> when the transition is in the table; otherwise <see langword="false"/>. <see cref="MemoryLifecycleState.Deleted"/> is never a transition target.</returns>
    public static bool IsAllowed(MemoryLifecycleState from, MemoryLifecycleState to) => (from, to) switch
    {
        (MemoryLifecycleState.Proposed, MemoryLifecycleState.Validated) => true,
        (MemoryLifecycleState.Proposed, MemoryLifecycleState.Rejected) => true,
        (MemoryLifecycleState.Validated, MemoryLifecycleState.Accepted) => true,
        (MemoryLifecycleState.Validated, MemoryLifecycleState.Rejected) => true,
        (MemoryLifecycleState.Accepted, MemoryLifecycleState.Active) => true,
        (MemoryLifecycleState.Accepted, MemoryLifecycleState.Rejected) => true,
        (MemoryLifecycleState.Active, MemoryLifecycleState.Corrected) => true,
        (MemoryLifecycleState.Active, MemoryLifecycleState.Expired) => true,
        _ => false,
    };

    /// <summary>Determines whether a record may be created in a state.</summary>
    /// <param name="state">The initial state.</param>
    /// <returns><see langword="true"/> for the non-terminal states; otherwise <see langword="false"/>.</returns>
    public static bool IsCreatable(MemoryLifecycleState state) =>
        state is MemoryLifecycleState.Proposed or MemoryLifecycleState.Validated or MemoryLifecycleState.Accepted or MemoryLifecycleState.Active;
}
