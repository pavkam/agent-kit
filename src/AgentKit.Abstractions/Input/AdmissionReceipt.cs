// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records the durable identity, lane, and sequence of one input admission or its equivalent idempotent replay.</summary>
/// <remarks>The receipt acknowledges acceptance only. It does not expose payload content or establish that the input was promoted, delivered to a model, or processed by a run.</remarks>
public sealed record AdmissionReceipt
{
    /// <summary>Initializes durable admission evidence.</summary>
    /// <param name="admissionId">The non-default durable identity assigned to the accepted admission.</param>
    /// <param name="inputId">The non-default caller-visible identity used to detect equivalent idempotent replay.</param>
    /// <param name="agentId">The non-default agent definition addressed by the admission.</param>
    /// <param name="sessionId">The non-default session that owns the durable admission record.</param>
    /// <param name="executionLaneId">The non-default resolved lane persisted with the admission and used for later promotion eligibility.</param>
    /// <param name="admittedSequence">The positive durable session sequence that orders this admission among eligible inputs.</param>
    /// <param name="existing"><see langword="true"/> when equivalent replay returned a prior acceptance; otherwise, <see langword="false"/> for a new admission.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default or the admission sequence is zero.</exception>
    public AdmissionReceipt(AdmissionId admissionId, InputId inputId, AgentId agentId, SessionId sessionId,
        ExecutionLaneId executionLaneId, SessionSequence admittedSequence, bool existing)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(admissionId, default); ArgumentOutOfRangeException.ThrowIfEqual(inputId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default); ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default); ArgumentOutOfRangeException.ThrowIfEqual(admittedSequence.Value, 0L, nameof(admittedSequence));
        AdmissionId = admissionId; InputId = inputId; AgentId = agentId; SessionId = sessionId;
        ExecutionLaneId = executionLaneId; AdmittedSequence = admittedSequence; Existing = existing;
    }
    /// <summary>Gets the durable identity of the accepted admission.</summary>
    /// <value>A non-default identity that remains stable across equivalent replay.</value>
    public AdmissionId AdmissionId { get; }
    /// <summary>Gets the caller-visible input identity used for idempotency.</summary>
    /// <value>A non-default identity; reuse with non-equivalent input must yield a conflict rather than replace this admission.</value>
    public InputId InputId { get; }
    /// <summary>Gets the agent definition addressed by the accepted input.</summary>
    /// <value>A non-default agent identity that scopes the admission and never grants authority.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets the session that owns the durable admission record.</summary>
    /// <value>A non-default session identity used with the lane to select later promotion work.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets the resolved execution lane persisted at admission time.</summary>
    /// <value>A non-default lane identity; promotion cannot move the input to a different lane.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets the durable sequence that orders the admission.</summary>
    /// <value>A positive session sequence used by captured promotion cutoffs, not a live run-event sequence.</value>
    public SessionSequence AdmittedSequence { get; }
    /// <summary>Gets whether this result acknowledges an existing equivalent admission.</summary>
    /// <value><see langword="true"/> for an equivalent replay that consumed no additional capacity; otherwise, <see langword="false"/>.</value>
    public bool Existing { get; }
}
