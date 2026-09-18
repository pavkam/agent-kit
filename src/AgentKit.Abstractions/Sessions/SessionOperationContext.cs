// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable operation context threaded through every session
/// coordinator and store call: which session and optional lane, whose causal
/// operation, on whose behalf, and under which captured authorization.
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
    /// <param name="executionLaneId">The lane for lane-owned work, or <see langword="null"/> for truthful session-wide work.</param>
    /// <param name="correlation">The causal operation performing this call.</param>
    /// <param name="identity">The identity on whose behalf this operation is performed.</param>
    /// <param name="authorization">The captured authorization evidence matching the identity, address, and operation.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="correlation"/>, <paramref name="identity"/>, or <paramref name="authorization"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A supplied identity is default.</exception>
    /// <exception cref="ArgumentException">The authorization evidence does not match the request.</exception>
    public SessionOperationContext(
        AgentId agentId,
        SessionId sessionId,
        ExecutionLaneId? executionLaneId,
        OperationCorrelation correlation,
        ExecutionIdentity identity,
        SecurityAuthorizationContext authorization)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        if (executionLaneId is { } laneId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(laneId, default, nameof(executionLaneId));
        }
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfInvalidInputAdmissionAuthorization(
            identity,
            agentId,
            sessionId,
            correlation,
            authorization,
            nameof(authorization));

        ExecutionLaneId = executionLaneId;
        Identity = identity;
        Authorization = authorization;
    }

    /// <summary>Gets the agent that owns the session.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound scope; never a second stored copy.</value>
    public AgentId AgentId => Authorization.Scope.AgentId;

    /// <summary>Gets the session this operation targets.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound scope; never a second stored copy.</value>
    public SessionId SessionId => Authorization.Scope.SessionId!.Value;

    /// <summary>Gets the lane for lane-owned work.</summary>
    /// <value>A non-default lane identity, or <see langword="null"/> for truthful session-wide work.</value>
    public ExecutionLaneId? ExecutionLaneId { get; }

    /// <summary>Gets the causal operation performing this call.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound scope; never a second stored copy.</value>
    public OperationCorrelation Correlation => Authorization.Scope.Correlation;

    /// <summary>Gets the identity on whose behalf this operation is performed.</summary>
    /// <value>The complete immutable trusted-ingress identity matching authorization evidence.</value>
    public ExecutionIdentity Identity { get; }

    /// <summary>Gets the captured authorization evidence for this operation.</summary>
    /// <value>Evidence matching this context's identity, address, and causal operation; it is not a consumable grant.</value>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Builds this context's complete session address.</summary>
    /// <returns>The validated agent/session pair used by authorized store routing.</returns>
    public SessionAddress ToAddress() => new(AgentId, SessionId);
}
