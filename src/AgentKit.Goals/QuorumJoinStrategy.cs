// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Is satisfied once the requested number of children are eligible.</summary>
/// <remarks>The yielded set is the eligible children in recorded ordinal order with the highest settlement sequence considered as the recorded cutoff. The join becomes unsatisfiable as soon as too few children remain that could still succeed, or once the wait cutoff passes without a quorum. The strategy is pure and thread-safe.</remarks>
internal sealed class QuorumJoinStrategy: IGoalJoinStrategy
{
    /// <inheritdoc/>
    public GoalJoinStrategyKey Key => GoalJoinStrategyKeys.Quorum;

    /// <inheritdoc/>
    public ValueTask<GoalJoinDecision> EvaluateAsync(GoalJoinEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Join.QuorumSize is not { } quorum)
        {
            return JoinStrategySupport.Done(new GoalJoinUnsatisfiable("A quorum join requires a quorum size."));
        }

        var eligible = JoinStrategySupport.Eligible(request.Children);
        if (eligible.Length >= quorum)
        {
            return JoinStrategySupport.Done(new GoalJoinSatisfied(eligible, null, JoinStrategySupport.Cutoff(eligible)));
        }

        var open = JoinStrategySupport.Open(request.Children);
        return JoinStrategySupport.Done(eligible.Length + open.Length < quorum
            ? new GoalJoinUnsatisfiable("Too few children remain that could still succeed to reach the quorum.")
            : request.DeadlineElapsed
                ? new GoalJoinUnsatisfiable("The wait cutoff passed before the quorum was reached.")
                : new GoalJoinPending(open));
    }
}
