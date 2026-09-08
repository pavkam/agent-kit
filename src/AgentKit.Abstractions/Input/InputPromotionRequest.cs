// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests atomic promotion against one exact active lane operation, captured branch state, and admission cutoff.</summary>
/// <remarks>The request carries revalidation evidence and an authorization context for a proposed mutation. It does not select input, consume admissions, or start a model request by itself.</remarks>
public sealed record InputPromotionRequest
{
    /// <summary>Initializes a promotion request for an already accepted active lane operation.</summary>
    /// <param name="agentId">The non-default agent definition owning the addressed operation.</param>
    /// <param name="sessionId">The non-default session that owns the lane and pending inputs.</param>
    /// <param name="executionLaneId">The non-default lane whose eligible inputs alone may be selected.</param>
    /// <param name="expectedOperation">The non-null correlation of the exact installed operation expected to receive promotion.</param>
    /// <param name="operationStateRevision">The non-default revision of the installed operation state to revalidate.</param>
    /// <param name="branchCursor">The non-null selected branch tip observed before the request was proposed.</param>
    /// <param name="cutoffSequence">The inclusive durable admission cutoff; inputs admitted later wait for a later boundary.</param>
    /// <param name="expectedVersion">Optional session compare-and-swap evidence, or <see langword="null"/> when the store does not require it.</param>
    /// <param name="expectedFencingToken">The distributed ownership fence, or <see langword="null"/> for process-local ownership.</param>
    /// <param name="identity">The non-null authenticated identity that must match <paramref name="authorization"/>.</param>
    /// <param name="authorization">The non-null authorization context matching the identity, address, and active run.</param>
    /// <param name="boundary">The defined safe loop boundary at which the selection may become visible to the run.</param>
    /// <param name="previousTurnId">The preceding committed turn, or <see langword="null"/> when the boundary has no prior turn.</param>
    /// <param name="targetTurnId">The non-default turn that would receive the promoted input in the atomic history transition.</param>
    /// <param name="maximumPromotions">The positive upper bound for a complete deterministic selection at this boundary.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity or revision is default, the boundary is undefined, a fence is default, or the bound is not positive.</exception>
    /// <exception cref="ArgumentException">Authorization identity, address, or active run does not match the request.</exception>
    public InputPromotionRequest(AgentId agentId, SessionId sessionId, ExecutionLaneId executionLaneId,
        InRunOperationCorrelation expectedOperation, OperationStateRevision operationStateRevision,
        SessionBranchCursor branchCursor, SessionSequence cutoffSequence, SessionVersion? expectedVersion,
        FencingToken? expectedFencingToken, ExecutionIdentity identity, SecurityAuthorizationContext authorization,
        PromotionBoundary boundary, TurnId? previousTurnId, TurnId targetTurnId, int maximumPromotions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default); ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default); ArgumentNullException.ThrowIfNull(expectedOperation);
        ArgumentOutOfRangeException.ThrowIfEqual(operationStateRevision, default); ArgumentNullException.ThrowIfNull(branchCursor);
        ArgumentNullException.ThrowIfNull(identity); ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfInvalidInputPromotionAuthorization(identity, agentId, sessionId, expectedOperation, authorization);
        ArgumentOutOfRangeException.ThrowIfUndefined(boundary);
        if (previousTurnId is { } previous)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(previous, default, nameof(previousTurnId));
        }
        ArgumentOutOfRangeException.ThrowIfEqual(targetTurnId, default); ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPromotions);
        ArgumentException.ThrowIfInvalidPromotionTurnBoundary(boundary, previousTurnId, targetTurnId);
        if (expectedFencingToken is { } fence)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(fence, default, nameof(expectedFencingToken));
        }
        AgentId = agentId; SessionId = sessionId; ExecutionLaneId = executionLaneId; ExpectedOperation = expectedOperation;
        OperationStateRevision = operationStateRevision; BranchCursor = branchCursor; CutoffSequence = cutoffSequence;
        ExpectedVersion = expectedVersion; ExpectedFencingToken = expectedFencingToken; Identity = identity;
        Authorization = authorization; Boundary = boundary; PreviousTurnId = previousTurnId; TargetTurnId = targetTurnId; MaximumPromotions = maximumPromotions;
    }
    /// <summary>Gets the agent definition owning the addressed operation.</summary>
    /// <value>A non-default identity used to scope revalidation and authorization.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets the session containing the operation and pending-input truth.</summary>
    /// <value>A non-default session identity; unrelated sessions cannot satisfy this request.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets the execution lane from which eligible input may be promoted.</summary>
    /// <value>A non-default lane identity; input from another lane must not be consumed.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets the exact installed operation expected to receive promotion.</summary>
    /// <value>A non-null correlation that prevents a stale request from advancing a successor operation.</value>
    public InRunOperationCorrelation ExpectedOperation { get; }
    /// <summary>Gets the installed operation-state revision expected by the request.</summary>
    /// <value>A non-default revision whose change invalidates affected promotion evidence.</value>
    public OperationStateRevision OperationStateRevision { get; }
    /// <summary>Gets the selected branch cursor expected by the request.</summary>
    /// <value>A non-null cursor that identifies the observed branch tip rather than a session-wide version.</value>
    public SessionBranchCursor BranchCursor { get; }
    /// <summary>Gets the inclusive durable admission cutoff for selection.</summary>
    /// <value>Only input admitted at or before this sequence is eligible; later input waits for another boundary.</value>
    public SessionSequence CutoffSequence { get; }
    /// <summary>Gets optional session compare-and-swap evidence.</summary>
    /// <value>The expected store version, or <see langword="null"/> when no version precondition applies.</value>
    public SessionVersion? ExpectedVersion { get; }
    /// <summary>Gets the optional distributed ownership fence.</summary>
    /// <value>A non-default fence token, or <see langword="null"/> when ownership is process-local.</value>
    public FencingToken? ExpectedFencingToken { get; }
    /// <summary>Gets the authenticated identity captured for the promotion request.</summary>
    /// <value>A non-null immutable identity; promotion must not substitute a current ambient principal.</value>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets authorization evidence for the exact promotion mutation.</summary>
    /// <value>A non-null context matching this request's identity, address, and active run; it grants no broader promotion.</value>
    public SecurityAuthorizationContext Authorization { get; }
    /// <summary>Gets the safe loop boundary at which promotion was requested.</summary>
    /// <value>A defined boundary that determines valid prior-turn shape and delivery eligibility.</value>
    public PromotionBoundary Boundary { get; }
    /// <summary>Gets the committed turn preceding the promotion boundary, when the boundary follows one.</summary>
    /// <value>A non-default prior turn identity, or <see langword="null"/> when no prior turn is meaningful at the boundary.</value>
    public TurnId? PreviousTurnId { get; }
    /// <summary>Gets the turn targeted to receive the promoted input.</summary>
    /// <value>A non-default turn identity that is part of the required atomic history transition.</value>
    public TurnId TargetTurnId { get; }
    /// <summary>Gets the upper bound for a complete promotion selection.</summary>
    /// <value>A positive maximum admission count that keeps selection deterministic and bounded.</value>
    public int MaximumPromotions { get; }
}
