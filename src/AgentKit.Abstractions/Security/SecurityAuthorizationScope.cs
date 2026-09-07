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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> or a present <paramref name="sessionId"/> is default.</exception>
    public SecurityAuthorizationScope(AgentId agentId, SessionId? sessionId, OperationCorrelation correlation)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        AgentId = agentId;
        SessionId = sessionId;
        Correlation = correlation;
    }

    /// <summary>Gets the agent identity.</summary>
    /// <value>The nondefault agent performing the protected operation.</value>
    /// <exception cref="ArgumentOutOfRangeException">An init assignment supplies a default value.</exception>
    public AgentId AgentId
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value.Value, Guid.Empty, "agentId");
            field = value;
        }
    }
    /// <summary>Gets the optional session identity.</summary>
    /// <value>The nondefault session when the operation belongs to one; otherwise <see langword="null"/>.</value>
    /// <exception cref="ArgumentOutOfRangeException">An init assignment supplies a present default value.</exception>
    public SessionId? SessionId
    {
        get;
        init
        {
            if (value is { } sessionId)
            {
                ArgumentOutOfRangeException.ThrowIfEqual(sessionId.Value, Guid.Empty, "sessionId");
            }

            field = value;
        }
    }
    /// <summary>Gets the causal operation correlation.</summary>
    /// <value>The nonnull correlation describing when and why the operation occurs.</value>
    /// <exception cref="ArgumentNullException">An init assignment supplies <see langword="null"/>.</exception>
    public OperationCorrelation Correlation
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, "correlation");
            field = value;
        }
    }
}
