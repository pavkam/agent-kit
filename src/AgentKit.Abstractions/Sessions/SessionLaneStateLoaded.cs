// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Returns one provisioned lane's current durable state.</summary>
public sealed record SessionLaneStateLoaded: SessionLaneStateResult
{
    /// <summary>Initializes a loaded result.</summary>
    /// <param name="state">The lane's current durable state.</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    public SessionLaneStateLoaded(SessionLaneState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;
    }

    /// <summary>Gets the lane's current durable state.</summary>
    /// <value>The exact retained revision, branch cursor, and optional accepted run.</value>
    public SessionLaneState State { get; }
}
