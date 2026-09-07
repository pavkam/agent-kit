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
    private readonly AgentId _agentId;
    private readonly SessionId _sessionId;
    private readonly RunId _runId;

    /// <summary>Initializes a new instance of the <see cref="SessionRunLeaseRequest"/> record.</summary>
    /// <param name="agentId">The agent that owns the session.</param>
    /// <param name="sessionId">The session to acquire the lease for.</param>
    /// <param name="runId">The run requesting exclusive mutating access.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="agentId"/>, <paramref name="sessionId"/>, or
    /// <paramref name="runId"/> is its default, empty identity.
    /// </exception>
    public SessionRunLeaseRequest(AgentId agentId, SessionId sessionId, RunId runId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default, nameof(sessionId));
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default, nameof(runId));

        _agentId = agentId;
        _sessionId = sessionId;
        _runId = runId;
    }

    /// <summary>Gets the agent that owns the session.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, empty identity.
    /// </exception>
    public AgentId AgentId
    {
        get => _agentId;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(AgentId));
            _agentId = value;
        }
    }

    /// <summary>Gets the session to acquire the lease for.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, empty identity.
    /// </exception>
    public SessionId SessionId
    {
        get => _sessionId;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(SessionId));
            _sessionId = value;
        }
    }

    /// <summary>Gets the run requesting exclusive mutating access.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, empty identity.
    /// </exception>
    public RunId RunId
    {
        get => _runId;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(RunId));
            _runId = value;
        }
    }
}
