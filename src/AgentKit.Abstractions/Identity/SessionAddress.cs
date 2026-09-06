// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The complete address of one session: the agent that owns it and the
/// session's own identity.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. A bare
/// <see cref="SessionId"/> is never sufficient to cross an isolation
/// boundary, because one engine hosts many agents concurrently and a
/// session identity is only unique within its owning agent's scope;
/// <see cref="SessionAddress"/> is the complete key every store, directory,
/// and coordinator operation actually addresses.
/// </remarks>
public sealed record SessionAddress
{
    /// <summary>Initializes a new instance of the <see cref="SessionAddress"/> record.</summary>
    /// <param name="agentId">The agent that owns the session.</param>
    /// <param name="sessionId">The session's own identity.</param>
    public SessionAddress(AgentId agentId, SessionId sessionId)
    {
        AgentId = agentId;
        SessionId = sessionId;
    }

    /// <summary>Gets the agent that owns the session.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the session's own identity.</summary>
    public SessionId SessionId { get; init; }
}
