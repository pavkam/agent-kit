// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Proves one durable input admission or an equivalent idempotent replay.</summary>
public sealed record AdmissionReceipt
{
    /// <summary>Initializes an admission receipt.</summary>
    public AdmissionReceipt(AdmissionId admissionId, InputId inputId, AgentId agentId, SessionId sessionId,
        ExecutionLaneId executionLaneId, SessionSequence admittedSequence, bool existing)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(admissionId, default); ArgumentOutOfRangeException.ThrowIfEqual(inputId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default); ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default); ArgumentOutOfRangeException.ThrowIfEqual(admittedSequence.Value, 0L, nameof(admittedSequence));
        AdmissionId = admissionId; InputId = inputId; AgentId = agentId; SessionId = sessionId;
        ExecutionLaneId = executionLaneId; AdmittedSequence = admittedSequence; Existing = existing;
    }
    /// <summary>Gets durable acceptance identity.</summary><value>A nondefault identity.</value>
    public AdmissionId AdmissionId { get; }
    /// <summary>Gets caller input identity.</summary><value>The idempotency identity.</value>
    public InputId InputId { get; }
    /// <summary>Gets owning agent.</summary><value>The addressed agent.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets owning session.</summary><value>The addressed session.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets resolved lane.</summary><value>The persisted lane.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets durable order.</summary><value>The positive admission sequence.</value>
    public SessionSequence AdmittedSequence { get; }
    /// <summary>Gets whether this receipt came from equivalent replay.</summary><value>True for an existing admission.</value>
    public bool Existing { get; }
}
