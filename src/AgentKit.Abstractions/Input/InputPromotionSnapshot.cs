// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures exact input selection and lane ownership evidence for atomic promotion revalidation.</summary>
public sealed record InputPromotionSnapshot
{
    /// <summary>Initializes a promotion snapshot.</summary>
    /// <param name="agentId">The owning agent.</param><param name="sessionId">The owning session.</param>
    /// <param name="executionLaneId">The expected lane.</param><param name="expectedOperation">The exact active operation.</param>
    /// <param name="operationStateRevision">The expected lane-operation revision.</param><param name="branchCursor">The expected branch tip.</param>
    /// <param name="cutoffSequence">The inclusive admission cutoff.</param><param name="expectedVersion">Optional session CAS evidence.</param>
    /// <param name="expectedFencingToken">The distributed fence, or null for process-local ownership.</param>
    /// <param name="boundary">The safe boundary.</param><param name="targetTurnId">The target turn.</param>
    /// <param name="admissionIds">The ordered unique selected admissions.</param>
    public InputPromotionSnapshot(AgentId agentId, SessionId sessionId, ExecutionLaneId executionLaneId,
        InRunOperationCorrelation expectedOperation, OperationStateRevision operationStateRevision,
        SessionBranchCursor branchCursor, SessionSequence cutoffSequence, SessionVersion? expectedVersion,
        FencingToken? expectedFencingToken, PromotionBoundary boundary, TurnId targetTurnId,
        ImmutableArray<AdmissionId> admissionIds)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default); ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default); ArgumentNullException.ThrowIfNull(expectedOperation);
        ArgumentOutOfRangeException.ThrowIfEqual(operationStateRevision, default); ArgumentNullException.ThrowIfNull(branchCursor);
        ArgumentOutOfRangeException.ThrowIfUndefined(boundary);
        ArgumentOutOfRangeException.ThrowIfEqual(targetTurnId, default);
        if (expectedFencingToken is { } fence)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(fence, default, nameof(expectedFencingToken));
        }
        ArgumentException.ThrowIfInvalidPromotionAdmissions(admissionIds);
        AgentId = agentId; SessionId = sessionId; ExecutionLaneId = executionLaneId; ExpectedOperation = expectedOperation;
        OperationStateRevision = operationStateRevision; BranchCursor = branchCursor; CutoffSequence = cutoffSequence;
        ExpectedVersion = expectedVersion; ExpectedFencingToken = expectedFencingToken; Boundary = boundary;
        TargetTurnId = targetTurnId; AdmissionIds = admissionIds;
    }
    /// <summary>Gets owning agent.</summary><value>The addressed agent.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets owning session.</summary><value>The addressed session.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets expected lane.</summary><value>The captured lane.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets expected operation.</summary><value>The exact in-run correlation.</value>
    public InRunOperationCorrelation ExpectedOperation { get; }
    /// <summary>Gets operation revision.</summary><value>The expected lane-operation state revision.</value>
    public OperationStateRevision OperationStateRevision { get; }
    /// <summary>Gets branch cursor.</summary><value>The expected branch and tip.</value>
    public SessionBranchCursor BranchCursor { get; }
    /// <summary>Gets admission cutoff.</summary><value>The inclusive durable sequence.</value>
    public SessionSequence CutoffSequence { get; }
    /// <summary>Gets optional CAS evidence.</summary><value>The observed session version, or null.</value>
    public SessionVersion? ExpectedVersion { get; }
    /// <summary>Gets ownership fence.</summary><value>The distributed fence, or null meaning process-local ownership.</value>
    public FencingToken? ExpectedFencingToken { get; }
    /// <summary>Gets safe boundary.</summary><value>The named boundary.</value>
    public PromotionBoundary Boundary { get; }
    /// <summary>Gets target turn.</summary><value>The turn receiving promoted content.</value>
    public TurnId TargetTurnId { get; }
    /// <summary>Gets selected admissions.</summary><value>Ordered unique admission identities.</value>
    public ImmutableArray<AdmissionId> AdmissionIds { get; }
}
