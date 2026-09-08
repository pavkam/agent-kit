// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies bounded eligible input and exact lane evidence to a side-effect-free promotion policy.</summary>
public sealed record InputPromotionContext
{
    /// <summary>Initializes a promotion context.</summary>
    /// <param name="agentId">The agent.</param><param name="sessionId">The session.</param><param name="executionLaneId">The lane.</param>
    /// <param name="expectedOperation">The active operation.</param><param name="operationStateRevision">Its revision.</param>
    /// <param name="branchCursor">The branch tip.</param><param name="cutoffSequence">The inclusive admission cutoff.</param>
    /// <param name="expectedVersion">Optional session CAS evidence.</param><param name="expectedFencingToken">Distributed fence, or null for local ownership.</param>
    /// <param name="boundary">The safe boundary.</param><param name="targetTurnId">The target turn.</param>
    /// <param name="eligible">The bounded immutable eligible snapshot.</param><param name="maximumPromotions">The positive plan bound.</param>
    public InputPromotionContext(AgentId agentId, SessionId sessionId, ExecutionLaneId executionLaneId,
        InRunOperationCorrelation expectedOperation, OperationStateRevision operationStateRevision,
        SessionBranchCursor branchCursor, SessionSequence cutoffSequence, SessionVersion? expectedVersion,
        FencingToken? expectedFencingToken, PromotionBoundary boundary, TurnId targetTurnId,
        ImmutableArray<AdmittedInput> eligible, int maximumPromotions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default); ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default); ArgumentNullException.ThrowIfNull(expectedOperation);
        ArgumentOutOfRangeException.ThrowIfEqual(operationStateRevision, default); ArgumentNullException.ThrowIfNull(branchCursor);
        ArgumentOutOfRangeException.ThrowIfUndefined(boundary);
        ArgumentOutOfRangeException.ThrowIfEqual(targetTurnId, default);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPromotions);
        if (expectedFencingToken is { } fence)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(fence, default, nameof(expectedFencingToken));
        }
        ArgumentException.ThrowIfInvalidPromotionEligibleInputs(eligible, agentId, sessionId, executionLaneId, cutoffSequence);
        AgentId = agentId; SessionId = sessionId; ExecutionLaneId = executionLaneId; ExpectedOperation = expectedOperation;
        OperationStateRevision = operationStateRevision; BranchCursor = branchCursor; CutoffSequence = cutoffSequence;
        ExpectedVersion = expectedVersion; ExpectedFencingToken = expectedFencingToken; Boundary = boundary;
        TargetTurnId = targetTurnId; Eligible = eligible; MaximumPromotions = maximumPromotions;
    }
    /// <summary>Gets agent.</summary><value>The addressed agent.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets session.</summary><value>The addressed session.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets lane.</summary><value>The expected lane.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets operation.</summary><value>The exact expected operation.</value>
    public InRunOperationCorrelation ExpectedOperation { get; }
    /// <summary>Gets operation revision.</summary><value>The expected positive revision.</value>
    public OperationStateRevision OperationStateRevision { get; }
    /// <summary>Gets branch cursor.</summary><value>The expected branch tip.</value>
    public SessionBranchCursor BranchCursor { get; }
    /// <summary>Gets cutoff.</summary><value>The inclusive durable admission sequence.</value>
    public SessionSequence CutoffSequence { get; }
    /// <summary>Gets optional CAS evidence.</summary><value>The session version, or null.</value>
    public SessionVersion? ExpectedVersion { get; }
    /// <summary>Gets ownership fence.</summary><value>The distributed fence, or null meaning local ownership.</value>
    public FencingToken? ExpectedFencingToken { get; }
    /// <summary>Gets safe boundary.</summary><value>The named loop boundary.</value>
    public PromotionBoundary Boundary { get; }
    /// <summary>Gets target turn.</summary><value>The turn receiving promoted content.</value>
    public TurnId TargetTurnId { get; }
    /// <summary>Gets eligible snapshot.</summary><value>Bounded immutable admitted inputs.</value>
    public ImmutableArray<AdmittedInput> Eligible { get; }
    /// <summary>Gets plan bound.</summary><value>The positive maximum admission count.</value>
    public int MaximumPromotions { get; }
}
