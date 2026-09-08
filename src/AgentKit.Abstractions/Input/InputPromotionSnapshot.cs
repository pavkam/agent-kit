// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures exact input selection, lane ownership, and target-turn evidence for atomic promotion revalidation.</summary>
/// <remarks>The snapshot is the immutable output of planning or reconciliation. It remains a proposal until the session owner accepts the matching transition, and it must not be used to consume input in another lane or turn.</remarks>
public sealed record InputPromotionSnapshot
{
    /// <summary>Initializes immutable selection evidence for one proposed promotion.</summary>
    /// <param name="agentId">The non-default agent definition owning the addressed operation.</param>
    /// <param name="sessionId">The non-default session that owns the selected admission records.</param>
    /// <param name="executionLaneId">The non-default lane from which all selected admissions originate.</param>
    /// <param name="expectedOperation">The non-null correlation of the exact active operation expected to receive the selection.</param>
    /// <param name="operationStateRevision">The non-default operation-state revision to revalidate before consumption.</param>
    /// <param name="branchCursor">The non-null selected branch tip observed for the proposal.</param>
    /// <param name="cutoffSequence">The inclusive durable admission cutoff under which the selection was formed.</param>
    /// <param name="expectedVersion">Optional session compare-and-swap evidence, or <see langword="null"/> when not required.</param>
    /// <param name="expectedFencingToken">The distributed ownership fence, or <see langword="null"/> for process-local ownership.</param>
    /// <param name="boundary">The defined safe boundary governing the promotion's turn shape.</param>
    /// <param name="previousTurnId">The preceding committed turn, or <see langword="null"/> when the boundary has none.</param>
    /// <param name="targetTurnId">The non-default turn to receive the selected input in the atomic history transition.</param>
    /// <param name="admissionIds">The non-default immutable sequence of uniquely selected admissions in durable selection order.</param>
    /// <exception cref="ArgumentNullException">The operation or branch cursor is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity or revision is default, the boundary is undefined, or a present fence is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="admissionIds"/> is default or contains default or duplicate identities.</exception>
    public InputPromotionSnapshot(AgentId agentId, SessionId sessionId, ExecutionLaneId executionLaneId,
        InRunOperationCorrelation expectedOperation, OperationStateRevision operationStateRevision,
        SessionBranchCursor branchCursor, SessionSequence cutoffSequence, SessionVersion? expectedVersion,
        FencingToken? expectedFencingToken, PromotionBoundary boundary, TurnId? previousTurnId, TurnId targetTurnId,
        ImmutableArray<AdmissionId> admissionIds)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default); ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default); ArgumentNullException.ThrowIfNull(expectedOperation);
        ArgumentOutOfRangeException.ThrowIfEqual(operationStateRevision, default); ArgumentNullException.ThrowIfNull(branchCursor);
        ArgumentOutOfRangeException.ThrowIfUndefined(boundary);
        if (previousTurnId is { } previous)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(previous, default, nameof(previousTurnId));
        }
        ArgumentOutOfRangeException.ThrowIfEqual(targetTurnId, default);
        ArgumentException.ThrowIfInvalidPromotionTurnBoundary(boundary, previousTurnId, targetTurnId);
        if (expectedFencingToken is { } fence)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(fence, default, nameof(expectedFencingToken));
        }
        ArgumentException.ThrowIfInvalidPromotionAdmissions(admissionIds);
        AgentId = agentId; SessionId = sessionId; ExecutionLaneId = executionLaneId; ExpectedOperation = expectedOperation;
        OperationStateRevision = operationStateRevision; BranchCursor = branchCursor; CutoffSequence = cutoffSequence;
        ExpectedVersion = expectedVersion; ExpectedFencingToken = expectedFencingToken; Boundary = boundary;
        PreviousTurnId = previousTurnId; TargetTurnId = targetTurnId; AdmissionIds = admissionIds;
    }
    /// <summary>Gets the agent definition owning the promotion proposal.</summary>
    /// <value>A non-default identity used to scope the revalidation evidence.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets the session that owns the selected admissions and operation.</summary>
    /// <value>A non-default session identity; the snapshot cannot be applied to another session.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets the lane expected to own every selected admission.</summary>
    /// <value>A non-default lane identity that prevents cross-lane consumption.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets the exact active operation expected to receive promotion.</summary>
    /// <value>A non-null in-run correlation; a successor operation invalidates the snapshot.</value>
    public InRunOperationCorrelation ExpectedOperation { get; }
    /// <summary>Gets the operation-state revision expected by the selection.</summary>
    /// <value>A non-default revision that forms part of atomic revalidation.</value>
    public OperationStateRevision OperationStateRevision { get; }
    /// <summary>Gets the branch cursor expected by the selection.</summary>
    /// <value>A non-null observed branch and tip, not a session-wide append version.</value>
    public SessionBranchCursor BranchCursor { get; }
    /// <summary>Gets the inclusive admission cutoff used to select <see cref="AdmissionIds"/>.</summary>
    /// <value>Only admissions at or before this durable sequence are eligible for this snapshot.</value>
    public SessionSequence CutoffSequence { get; }
    /// <summary>Gets optional session compare-and-swap evidence.</summary>
    /// <value>The expected store version, or <see langword="null"/> when no version precondition applies.</value>
    public SessionVersion? ExpectedVersion { get; }
    /// <summary>Gets the optional distributed ownership fence.</summary>
    /// <value>A non-default fencing token, or <see langword="null"/> for process-local ownership.</value>
    public FencingToken? ExpectedFencingToken { get; }
    /// <summary>Gets the safe loop boundary that produced the selection.</summary>
    /// <value>A defined boundary that determines the valid prior-turn and target-turn relationship.</value>
    public PromotionBoundary Boundary { get; }
    /// <summary>Gets the prior committed turn when the safe boundary follows one.</summary>
    /// <value>A non-default prior turn identity, or <see langword="null"/> when the boundary has no preceding turn.</value>
    public TurnId? PreviousTurnId { get; }
    /// <summary>Gets the target turn to receive the selected input.</summary>
    /// <value>A non-default turn identity that must be committed with the promotion marker and consumed admission IDs.</value>
    public TurnId TargetTurnId { get; }
    /// <summary>Gets the selected admission identities in deterministic order.</summary>
    /// <value>A non-default immutable collection of unique non-default identities; it is selection evidence, not proof of consumption.</value>
    public ImmutableArray<AdmissionId> AdmissionIds { get; }
}
