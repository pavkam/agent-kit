// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests authorized, idempotent admission of immutable caller input into one resolved session lane.</summary>
/// <remarks>This request supplies admission evidence only. It neither appends queue state nor starts a run, and its optional version is compared only by the selected session owner.</remarks>
public sealed record InputAdmissionRequest
{
    /// <summary>Initializes an admission request for one resolved address and immutable caller identity.</summary>
    /// <param name="agentId">The non-default agent definition addressed by the input.</param>
    /// <param name="sessionId">The non-default session to which input is admitted.</param>
    /// <param name="executionLaneId">The non-default lane resolved before admission and persisted if acceptance succeeds.</param>
    /// <param name="identity">The non-null authenticated caller identity that must match <paramref name="authorization"/>.</param>
    /// <param name="correlation">The non-null causal operation evidence for this request.</param>
    /// <param name="authorization">The non-null authorization context matching the identity, address, and causal operation.</param>
    /// <param name="input">The non-null immutable caller input to validate, preprocess, and admit idempotently.</param>
    /// <param name="expectedVersion">Optional session compare-and-swap evidence, or <see langword="null"/> when the selected contract does not require it.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="identity"/> differs from the identity captured by <paramref name="authorization"/>.</exception>
    public InputAdmissionRequest(AgentId agentId, SessionId sessionId, ExecutionLaneId executionLaneId,
        ExecutionIdentity identity, OperationCorrelation correlation, SecurityAuthorizationContext authorization,
        AgentInput input, SessionVersion? expectedVersion = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default); ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default); ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(correlation); ArgumentNullException.ThrowIfNull(authorization); ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfInvalidInputAdmissionAuthorization(identity, agentId, sessionId, correlation, authorization);
        AgentId = agentId; SessionId = sessionId; ExecutionLaneId = executionLaneId; Identity = identity;
        Correlation = correlation; Authorization = authorization; Input = input; ExpectedVersion = expectedVersion;
    }
    /// <summary>Gets the agent definition addressed by the admission.</summary>
    /// <value>A non-default identity used with the session and lane to scope authorization and admission.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets the session to which the caller asks to admit input.</summary>
    /// <value>A non-default session identity; successful admission appends only to this session's durable truth.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets the execution lane resolved before admission.</summary>
    /// <value>A non-default lane identity persisted on successful admission; later promotion cannot select a different lane.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets the authenticated identity captured for admission.</summary>
    /// <value>A non-null immutable identity that must equal the identity asserted by <see cref="Authorization"/>.</value>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets causal operation evidence for the admission request.</summary>
    /// <value>A non-null correlation used to bind authorization and audit evidence without creating a run identity.</value>
    public OperationCorrelation Correlation { get; }
    /// <summary>Gets the authorization context captured for the addressed admission.</summary>
    /// <value>A non-null security evidence value; possession does not authorize a different identity, session, lane, or operation.</value>
    public SecurityAuthorizationContext Authorization { get; }
    /// <summary>Gets the immutable caller input requested for admission.</summary>
    /// <value>A non-null payload retaining idempotency identity and delivery class; acceptance is determined later by the coordinator or queue.</value>
    public AgentInput Input { get; }
    /// <summary>Gets optional session compare-and-swap evidence observed by the caller.</summary>
    /// <value>The expected session version, or <see langword="null"/> when no version precondition accompanies this request.</value>
    public SessionVersion? ExpectedVersion { get; }
}
