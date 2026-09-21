// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Event arguments whose dispatching stage has already established the agent, and optionally the session, the
/// invocation relates to.
/// </summary>
public interface IAgentScopedHookStage
{
    /// <summary>Gets the agent this hook invocation occurred for.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the session this hook invocation relates to, when applicable.</summary>
    public SessionId? SessionId { get; }
}
