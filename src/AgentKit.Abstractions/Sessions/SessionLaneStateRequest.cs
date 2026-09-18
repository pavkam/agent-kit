// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests authorized discovery of one execution lane's current durable state before a run is accepted.</summary>
public sealed record SessionLaneStateRequest
{
    /// <summary>Initializes a lane-state discovery request.</summary>
    /// <param name="context">The lane-bound before-run context naming the lane to inspect.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">The context is not lane-bound or not before-run.</exception>
    public SessionLaneStateRequest(SessionOperationContext context)
    {
        ArgumentException.ThrowIfSessionContextNotLaneBound(context);
        ArgumentException.ThrowIfSessionContextNotBeforeRun(context);
        Context = context;
    }

    /// <summary>Gets the exact context.</summary>
    /// <value>A lane-bound before-run context used to discover a lane without an accepted run.</value>
    public SessionOperationContext Context { get; }
}
