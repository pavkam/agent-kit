// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a hook point may not be entered because doing so would exceed the permitted reentrant depth.</summary>
public sealed record HookInvocationTrackingRejected: HookInvocationTrackingResult
{
    /// <summary>Initializes a rejected entry result.</summary>
    /// <param name="activeDepth">The reentrant dispatch depth already active for the attempted hook point.</param>
    /// <param name="permittedDepth">The maximum depth permitted for the attempted hook point.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="activeDepth"/> or <paramref name="permittedDepth"/> is negative.</exception>
    public HookInvocationTrackingRejected(int activeDepth, int permittedDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(activeDepth);
        ArgumentOutOfRangeException.ThrowIfNegative(permittedDepth);
        ActiveDepth = activeDepth;
        PermittedDepth = permittedDepth;
    }

    /// <summary>Gets the reentrant dispatch depth already active for the attempted hook point.</summary>
    public int ActiveDepth { get; }

    /// <summary>Gets the maximum depth permitted for the attempted hook point.</summary>
    public int PermittedDepth { get; }
}
