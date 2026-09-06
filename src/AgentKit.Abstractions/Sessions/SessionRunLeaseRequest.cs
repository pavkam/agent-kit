// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A request to acquire the single active mutating run for a session.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record SessionRunLeaseRequest
{
    /// <summary>Initializes a new instance of the <see cref="SessionRunLeaseRequest"/> record.</summary>
    /// <param name="agentId">The agent that owns the session.</param>
    /// <param name="sessionId">The session to acquire the lease for.</param>
    /// <param name="runId">The run requesting exclusive mutating access.</param>
    public SessionRunLeaseRequest(AgentId agentId, SessionId sessionId, RunId runId)
    {
        AgentId = agentId;
        SessionId = sessionId;
        RunId = runId;
    }

    /// <summary>Gets the agent that owns the session.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the session to acquire the lease for.</summary>
    public SessionId SessionId { get; init; }

    /// <summary>Gets the run requesting exclusive mutating access.</summary>
    public RunId RunId { get; init; }
}
