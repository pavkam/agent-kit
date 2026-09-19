// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a hook point may be entered at the returned reentrant depth.</summary>
public sealed record HookInvocationTrackingEntered: HookInvocationTrackingResult
{
    /// <summary>Initializes a successful entry result.</summary>
    /// <param name="depth">The reentrant dispatch depth now active for the attempted hook point, at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="depth"/> is less than 1.</exception>
    public HookInvocationTrackingEntered(int depth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(depth, 1);
        Depth = depth;
    }

    /// <summary>Gets the reentrant dispatch depth now active for the attempted hook point.</summary>
    public int Depth { get; }
}
