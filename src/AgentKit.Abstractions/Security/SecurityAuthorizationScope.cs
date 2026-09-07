// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds protected work to one agent, optional session, and truthful causal operation.</summary>
public sealed record SecurityAuthorizationScope
{
    /// <summary>Initializes an authorization scope.</summary>
    /// <param name="agentId">The agent performing the operation.</param>
    /// <param name="sessionId">The session when the operation truthfully belongs to one.</param>
    /// <param name="correlation">The causal operation correlation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="correlation"/> is null.</exception>
    public SecurityAuthorizationScope(AgentId agentId, SessionId? sessionId, OperationCorrelation correlation)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        AgentId = agentId;
        SessionId = sessionId;
        Correlation = correlation;
    }

    /// <summary>Gets the agent identity.</summary>
    public AgentId AgentId { get; init; }
    /// <summary>Gets the optional session identity.</summary>
    public SessionId? SessionId { get; init; }
    /// <summary>Gets the causal operation correlation.</summary>
    public OperationCorrelation Correlation { get; init; }
}
