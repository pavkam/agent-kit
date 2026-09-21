// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the run identities and authorization used to build a legacy catalog capture.</summary>
public sealed record RunToolCatalogCaptureRequest
{
    /// <summary>Initializes run catalog capture evidence.</summary>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="sessionId">The owning session.</param>
    /// <param name="runId">The active run.</param>
    /// <param name="authorization">The captured authorization for tool execution.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="authorization"/> is null.</exception>
    public RunToolCatalogCaptureRequest(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        SecurityAuthorizationContext authorization)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentNullException.ThrowIfNull(authorization);
        AgentId = agentId;
        SessionId = sessionId;
        RunId = runId;
        Authorization = authorization;
    }

    /// <summary>Gets the owning agent.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the owning session.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the active run.</summary>
    public RunId RunId { get; }

    /// <summary>Gets the captured authorization.</summary>
    public SecurityAuthorizationContext Authorization { get; }
}
