// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Thrown when a turn is submitted to a session that is already running one and the pinned session profile's
/// <see cref="SessionBusyBehavior"/> is <see cref="SessionBusyBehavior.Reject"/>.
/// </summary>
/// <remarks>
/// The rejected turn had no durable effect: nothing was appended and no run was allocated. The caller may retry
/// after the active run settles; a host that prefers queuing selects <see cref="SessionBusyBehavior.Wait"/> in the
/// session profile instead.
/// </remarks>
public sealed class AgentSessionBusyException: InvalidOperationException
{
    /// <summary>Initializes the exception for one busy session.</summary>
    /// <param name="agentId">The agent whose session is busy.</param>
    /// <param name="sessionId">The busy session.</param>
    /// <param name="activeRunId">The run currently holding the session.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is the default value.</exception>
    public AgentSessionBusyException(AgentId agentId, SessionId sessionId, RunId activeRunId)
        : base($"Session {sessionId} of agent {agentId} is busy with run {activeRunId}; the session profile rejects concurrent turns.")
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(activeRunId, default);
        AgentId = agentId;
        SessionId = sessionId;
        ActiveRunId = activeRunId;
    }

    /// <summary>Gets the agent whose session is busy.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the busy session.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the run currently holding the session.</summary>
    public RunId ActiveRunId { get; }
}
