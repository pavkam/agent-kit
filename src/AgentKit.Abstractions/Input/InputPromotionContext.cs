// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies bounded eligible input and exact lane-state evidence to a side-effect-free promotion policy.</summary>
/// <remarks>The context is an immutable planning snapshot. A policy may select from <see cref="Eligible"/>, but it cannot mutate the queue, consume an admission, or rely on state that was not captured here.</remarks>
public sealed record InputPromotionContext
{
    /// <summary>Initializes immutable evidence for one promotion-planning evaluation.</summary>
    /// <param name="agentId">The non-default agent definition owning the addressed operation.</param>
    /// <param name="sessionId">The non-default session containing the operation and its pending input.</param>
    /// <param name="executionLaneId">The non-default lane from which all eligible inputs must originate.</param>
    /// <param name="expectedOperation">The non-null correlation of the active operation expected to receive promotion.</param>
    /// <param name="operationStateRevision">The non-default active-operation revision captured for revalidation.</param>
    /// <param name="branchCursor">The non-null branch tip observed for the selection.</param>
    /// <param name="cutoffSequence">The inclusive durable admission cutoff represented by <paramref name="eligible"/>.</param>
    /// <param name="expectedVersion">Optional store compare-and-swap evidence, or <see langword="null"/> when not required.</param>
    /// <param name="expectedFencingToken">The distributed ownership fence, or <see langword="null"/> for process-local ownership.</param>
    /// <param name="boundary">The defined safe boundary governing the valid prior-turn shape and promotion semantics.</param>
    /// <param name="previousTurnId">The preceding committed turn, or <see langword="null"/> when the boundary has no prior turn.</param>
    /// <param name="targetTurnId">The non-default turn that an accepted promotion would update atomically.</param>
    /// <param name="eligible">The non-default bounded immutable snapshot of unpromoted, lane-matching inputs admitted no later than the cutoff.</param>
    /// <param name="maximumPromotions">The positive maximum number of admissions the policy may select in a complete plan.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity or revision is default, the boundary is undefined, the fence is default, or the plan bound is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="eligible"/> is default, contains null or duplicate admissions, or includes promoted, post-cutoff, or differently addressed input.</exception>
    public InputPromotionContext(AgentId agentId, SessionId sessionId, ExecutionLaneId executionLaneId,
        InRunOperationCorrelation expectedOperation, OperationStateRevision operationStateRevision,
        SessionBranchCursor branchCursor, SessionSequence cutoffSequence, SessionVersion? expectedVersion,
        FencingToken? expectedFencingToken, PromotionBoundary boundary, TurnId? previousTurnId, TurnId targetTurnId,
        ImmutableArray<AdmittedInput> eligible, int maximumPromotions)
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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPromotions);
        if (expectedFencingToken is { } fence)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(fence, default, nameof(expectedFencingToken));
        }
        ArgumentException.ThrowIfInvalidPromotionEligibleInputs(eligible, agentId, sessionId, executionLaneId, cutoffSequence);
        AgentId = agentId; SessionId = sessionId; ExecutionLaneId = executionLaneId; ExpectedOperation = expectedOperation;
        OperationStateRevision = operationStateRevision; BranchCursor = branchCursor; CutoffSequence = cutoffSequence;
        ExpectedVersion = expectedVersion; ExpectedFencingToken = expectedFencingToken; Boundary = boundary;
        PreviousTurnId = previousTurnId; TargetTurnId = targetTurnId; Eligible = eligible; MaximumPromotions = maximumPromotions;
    }
    /// <summary>Gets the agent definition owning the evaluated operation.</summary>
    /// <value>A non-default identity used to scope planning evidence.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets the session containing this promotion-planning snapshot.</summary>
    /// <value>A non-default session identity that owns the pending-input truth.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets the lane from which the policy may select input.</summary>
    /// <value>A non-default execution lane identity; inputs from another lane are not eligible.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets the exact active operation expected to receive a promotion.</summary>
    /// <value>A non-null operation correlation that prevents planning for a successor operation.</value>
    public InRunOperationCorrelation ExpectedOperation { get; }
    /// <summary>Gets the active-operation state revision captured for revalidation.</summary>
    /// <value>A non-default revision that identifies state relevant to the planned promotion.</value>
    public OperationStateRevision OperationStateRevision { get; }
    /// <summary>Gets the selected branch cursor captured for planning.</summary>
    /// <value>A non-null branch-and-tip observation, separate from any store-wide version token.</value>
    public SessionBranchCursor BranchCursor { get; }
    /// <summary>Gets the inclusive durable admission cutoff used to form <see cref="Eligible"/>.</summary>
    /// <value>Only inputs admitted at or before this sequence appear eligible; later arrivals wait for another boundary.</value>
    public SessionSequence CutoffSequence { get; }
    /// <summary>Gets optional store compare-and-swap evidence.</summary>
    /// <value>The expected session version, or <see langword="null"/> when the selected store contract does not require it.</value>
    public SessionVersion? ExpectedVersion { get; }
    /// <summary>Gets the optional distributed ownership fence.</summary>
    /// <value>A non-default fencing token, or <see langword="null"/> when the operation has process-local ownership.</value>
    public FencingToken? ExpectedFencingToken { get; }
    /// <summary>Gets the safe loop boundary at which planning occurs.</summary>
    /// <value>A defined boundary that governs whether <see cref="PreviousTurnId"/> is meaningful and when input may become visible.</value>
    public PromotionBoundary Boundary { get; }
    /// <summary>Gets the prior committed turn when the boundary follows one.</summary>
    /// <value>A non-default turn identity, or <see langword="null"/> for a boundary that has no prior turn.</value>
    public TurnId? PreviousTurnId { get; }
    /// <summary>Gets the target turn that an accepted promotion would receive.</summary>
    /// <value>A non-default turn identity included in the eventual atomic history transition.</value>
    public TurnId TargetTurnId { get; }
    /// <summary>Gets the bounded immutable inputs eligible for policy selection.</summary>
    /// <value>A non-default immutable collection of unpromoted, cutoff-eligible admissions in durable order; it is not a live queue view.</value>
    public ImmutableArray<AdmittedInput> Eligible { get; }
    /// <summary>Gets the maximum number of admissions the policy may select.</summary>
    /// <value>A positive bound that limits a complete deterministic plan.</value>
    public int MaximumPromotions { get; }
}
