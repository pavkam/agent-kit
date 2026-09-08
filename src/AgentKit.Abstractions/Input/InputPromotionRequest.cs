// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests atomic promotion for one exact lane operation and captured branch state.</summary>
public sealed record InputPromotionRequest
{
    /// <summary>Initializes a promotion request.</summary>
    public InputPromotionRequest(AgentId agentId, SessionId sessionId, ExecutionLaneId executionLaneId,
        InRunOperationCorrelation expectedOperation, OperationStateRevision operationStateRevision,
        SessionBranchCursor branchCursor, SessionSequence cutoffSequence, SessionVersion? expectedVersion,
        FencingToken? expectedFencingToken, ExecutionIdentity identity, SecurityAuthorizationContext authorization,
        PromotionBoundary boundary, TurnId targetTurnId, int maximumPromotions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default); ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default); ArgumentNullException.ThrowIfNull(expectedOperation);
        ArgumentOutOfRangeException.ThrowIfEqual(operationStateRevision, default); ArgumentNullException.ThrowIfNull(branchCursor);
        ArgumentNullException.ThrowIfNull(identity); ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfInputAuthorizationIdentityMismatch(identity, authorization);
        ArgumentOutOfRangeException.ThrowIfUndefined(boundary);
        ArgumentOutOfRangeException.ThrowIfEqual(targetTurnId, default); ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPromotions);
        if (expectedFencingToken is { } fence)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(fence, default, nameof(expectedFencingToken));
        }
        AgentId = agentId; SessionId = sessionId; ExecutionLaneId = executionLaneId; ExpectedOperation = expectedOperation;
        OperationStateRevision = operationStateRevision; BranchCursor = branchCursor; CutoffSequence = cutoffSequence;
        ExpectedVersion = expectedVersion; ExpectedFencingToken = expectedFencingToken; Identity = identity;
        Authorization = authorization; Boundary = boundary; TargetTurnId = targetTurnId; MaximumPromotions = maximumPromotions;
    }
    /// <summary>Gets agent.</summary><value>The addressed agent.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets session.</summary><value>The addressed session.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets lane.</summary><value>The expected lane.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets operation.</summary><value>The expected active operation.</value>
    public InRunOperationCorrelation ExpectedOperation { get; }
    /// <summary>Gets operation revision.</summary><value>The expected lane-state revision.</value>
    public OperationStateRevision OperationStateRevision { get; }
    /// <summary>Gets branch cursor.</summary><value>The expected branch tip.</value>
    public SessionBranchCursor BranchCursor { get; }
    /// <summary>Gets cutoff.</summary><value>The inclusive admission sequence.</value>
    public SessionSequence CutoffSequence { get; }
    /// <summary>Gets optional CAS evidence.</summary><value>The session version, or null.</value>
    public SessionVersion? ExpectedVersion { get; }
    /// <summary>Gets fence.</summary><value>The distributed fence, or null meaning local ownership.</value>
    public FencingToken? ExpectedFencingToken { get; }
    /// <summary>Gets execution identity.</summary><value>The authenticated immutable identity.</value>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets authorization evidence.</summary><value>The captured security context.</value>
    public SecurityAuthorizationContext Authorization { get; }
    /// <summary>Gets safe boundary.</summary><value>The named boundary.</value>
    public PromotionBoundary Boundary { get; }
    /// <summary>Gets target turn.</summary><value>The receiving turn.</value>
    public TurnId TargetTurnId { get; }
    /// <summary>Gets plan bound.</summary><value>The positive maximum selection count.</value>
    public int MaximumPromotions { get; }
}
