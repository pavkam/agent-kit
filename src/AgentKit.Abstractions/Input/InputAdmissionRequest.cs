// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests authorized, idempotent admission into one resolved session lane.</summary>
public sealed record InputAdmissionRequest
{
    /// <summary>Initializes an admission request.</summary>
    /// <param name="agentId">The addressed agent.</param><param name="sessionId">The addressed session.</param>
    /// <param name="executionLaneId">The resolved lane.</param><param name="identity">The authenticated caller identity.</param>
    /// <param name="correlation">The causal operation.</param><param name="authorization">The captured authorization context.</param>
    /// <param name="input">The prevalidated caller input.</param><param name="expectedVersion">Optional session CAS evidence.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    public InputAdmissionRequest(AgentId agentId, SessionId sessionId, ExecutionLaneId executionLaneId,
        ExecutionIdentity identity, OperationCorrelation correlation, SecurityAuthorizationContext authorization,
        AgentInput input, SessionVersion? expectedVersion = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default); ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default); ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(correlation); ArgumentNullException.ThrowIfNull(authorization); ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfInputAuthorizationIdentityMismatch(identity, authorization);
        AgentId = agentId; SessionId = sessionId; ExecutionLaneId = executionLaneId; Identity = identity;
        Correlation = correlation; Authorization = authorization; Input = input; ExpectedVersion = expectedVersion;
    }
    /// <summary>Gets addressed agent.</summary><value>The nondefault agent.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets addressed session.</summary><value>The nondefault session.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets resolved lane.</summary><value>The lane persisted on acceptance.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets caller identity.</summary><value>The complete authenticated identity.</value>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets operation correlation.</summary><value>The causal operation.</value>
    public OperationCorrelation Correlation { get; }
    /// <summary>Gets authorization context.</summary><value>The captured security evidence.</value>
    public SecurityAuthorizationContext Authorization { get; }
    /// <summary>Gets caller input.</summary><value>The immutable input to preprocess and admit.</value>
    public AgentInput Input { get; }
    /// <summary>Gets optional CAS evidence.</summary><value>The expected session version, or null.</value>
    public SessionVersion? ExpectedVersion { get; }
}
