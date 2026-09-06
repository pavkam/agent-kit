// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable operation context threaded through every session
/// coordinator and store call: which session, whose causal operation, and
/// on whose behalf.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. A bare <see cref="SessionId"/> is never
/// sufficient to identify a session in an engine that hosts many agents;
/// every operation carries the complete <see cref="SessionAddress"/> plus
/// the <see cref="OperationCorrelation"/> that lets audit and recovery trace
/// the effect back to its cause.
/// </remarks>
public sealed record SessionOperationContext
{
    /// <summary>Initializes a new instance of the <see cref="SessionOperationContext"/> record.</summary>
    /// <param name="agentId">The agent that owns the session.</param>
    /// <param name="sessionId">The session this operation targets.</param>
    /// <param name="correlation">The causal operation performing this call.</param>
    /// <param name="identity">The identity on whose behalf this operation is performed.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="correlation"/> or <paramref name="identity"/> is null.
    /// </exception>
    public SessionOperationContext(
        AgentId agentId,
        SessionId sessionId,
        OperationCorrelation correlation,
        ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(identity);

        AgentId = agentId;
        SessionId = sessionId;
        Correlation = correlation;
        Identity = identity;
    }

    /// <summary>Gets the agent that owns the session.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the session this operation targets.</summary>
    public SessionId SessionId { get; init; }

    /// <summary>Gets the causal operation performing this call.</summary>
    public OperationCorrelation Correlation { get; init; }

    /// <summary>Gets the identity on whose behalf this operation is performed.</summary>
    public ExecutionIdentity Identity { get; init; }

    /// <summary>Gets this context's session as a complete <see cref="SessionAddress"/>.</summary>
    public SessionAddress ToAddress() => new(AgentId, SessionId);
}
