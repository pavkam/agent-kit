// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests authorized discovery of one execution lane's currently pending, not-yet-promoted admissions.</summary>
/// <remarks>
/// This is the truthful discovery evidence a caller needs before proposing an <see cref="SessionInputPromotionRequest"/>: which
/// durable admissions on this lane still await promotion, in admission order. Discovery does not consume, reserve, or
/// otherwise mutate any admission; a proposal built from this evidence is still revalidated atomically at commit time.
/// </remarks>
public sealed record SessionPendingInputsRequest
{
    /// <summary>Initializes a pending-input discovery request.</summary>
    /// <param name="context">The lane-bound context naming the lane to inspect, before or during an active run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">The context is not lane-bound.</exception>
    public SessionPendingInputsRequest(SessionOperationContext context)
    {
        ArgumentException.ThrowIfSessionContextNotLaneBound(context);
        Context = context;
    }

    /// <summary>Gets the exact context.</summary>
    /// <value>A lane-bound in-run context used to discover pending admissions for the active operation's lane.</value>
    public SessionOperationContext Context { get; }
}
