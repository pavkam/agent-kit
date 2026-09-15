// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one bounded page of visible session routes for an agent and owner.</summary>
public sealed record SessionDirectoryListRequest
{
    /// <summary>Initializes a bounded directory scan.</summary>
    /// <param name="agentId">The non-default agent whose sessions are requested.</param>
    /// <param name="identity">The complete caller identity defining tenant and owner visibility.</param>
    /// <param name="authorization">Captured sessionless authorization for this scan.</param>
    /// <param name="afterSessionId">The exclusive stable continuation cursor.</param>
    /// <param name="maximumResults">The positive page-size bound.</param>
    /// <exception cref="ArgumentNullException">A reference parameter is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> is default or <paramref name="maximumResults"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="authorization"/> does not match the agent, identity, and sessionless scope.</exception>
    public SessionDirectoryListRequest(AgentId agentId, ExecutionIdentity identity,
        SecurityAuthorizationContext authorization, SessionId? afterSessionId, int maximumResults)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfNotEqual(authorization.Identity, identity, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Scope.AgentId, agentId, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Scope.SessionId, null, nameof(authorization));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResults);
        AgentId = agentId; Identity = identity; Authorization = authorization;
        AfterSessionId = afterSessionId; MaximumResults = maximumResults;
    }

    /// <summary>Gets the owning agent.</summary><value>A non-default typed identity.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets the complete requesting identity.</summary><value>The tenant and principal visibility boundary.</value>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets captured sessionless authorization.</summary><value>Evidence only; the directory still consumes an exact grant.</value>
    public SecurityAuthorizationContext Authorization { get; }
    /// <summary>Gets the exclusive continuation cursor.</summary><value>Null for the first page.</value>
    public SessionId? AfterSessionId { get; }
    /// <summary>Gets the requested page bound.</summary><value>A positive count.</value>
    public int MaximumResults { get; }
}
