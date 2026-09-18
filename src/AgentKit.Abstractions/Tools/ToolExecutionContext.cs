// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable operation context threaded through one tool invocation
/// attempt: which call, on whose behalf, and within which causal operation.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. <see cref="ToolCallId"/> is the same identity
/// that flows through the model-facing <see cref="ToolCallPart"/> and its
/// terminal <see cref="ToolResultPart"/>, so a tool invocation is always
/// traceable back to the exact call that requested it.
/// </remarks>
public sealed record ToolExecutionContext
{
    /// <summary>Initializes a new instance of the <see cref="ToolExecutionContext"/> record.</summary>
    /// <param name="agentId">The agent this invocation occurred for.</param>
    /// <param name="sessionId">The session this invocation occurred within, when applicable.</param>
    /// <param name="toolCallId">The call this invocation answers.</param>
    /// <param name="correlation">The causal operation performing this invocation.</param>
    /// <param name="identity">The identity on whose behalf this invocation is performed.</param>
    /// <param name="authorization">The exact captured authorization for this tool invocation.</param>
    /// <param name="sessionProfile">The immutable session profile when this invocation may access a session; null for sessionless tools.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="correlation"/>, <paramref name="identity"/>, or
    /// <paramref name="authorization"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="authorization"/> does not exactly match the supplied agent,
    /// optional session, correlation, and complete execution identity.
    /// </exception>
    public ToolExecutionContext(
        AgentId agentId,
        SessionId? sessionId,
        ToolCallId toolCallId,
        OperationCorrelation correlation,
        ExecutionIdentity identity,
        SecurityAuthorizationContext authorization,
        SessionProfileSnapshot? sessionProfile)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfInvalidOperationAuthorization(identity, agentId, sessionId, correlation,
            authorization);

        ToolCallId = toolCallId;
        Identity = identity;
        Authorization = authorization;
        SessionProfile = sessionProfile;
    }

    /// <summary>Gets the agent this invocation occurred for.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound scope; never a second stored copy.</value>
    public AgentId AgentId => Authorization.Scope.AgentId;

    /// <summary>Gets the session this invocation occurred within, when applicable.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound scope; never a second stored copy.</value>
    public SessionId? SessionId => Authorization.Scope.SessionId;

    /// <summary>Gets the call this invocation answers.</summary>
    public ToolCallId ToolCallId { get; }

    /// <summary>Gets the causal operation performing this invocation.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound scope; never a second stored copy.</value>
    public OperationCorrelation Correlation => Authorization.Scope.Correlation;

    /// <summary>Gets the identity on whose behalf this invocation is performed.</summary>
    public ExecutionIdentity Identity { get; }

    /// <summary>Gets the exact authorization captured for this tool invocation.</summary>
    /// <value>Immutable evidence matching the complete identity, address, and causal correlation.</value>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets the immutable session profile available to session-backed tools.</summary>
    /// <value>The compiled profile for session access, or null for a sessionless invocation.</value>
    public SessionProfileSnapshot? SessionProfile { get; }
}
