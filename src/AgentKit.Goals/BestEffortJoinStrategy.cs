// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Yields whatever succeeded once every child settles or the wait cutoff is reached.</summary>
/// <remarks>The yielded set is the eligible children in recorded ordinal order and may be empty; this join never reports unsatisfiable. The strategy is pure and thread-safe.</remarks>
internal sealed class BestEffortJoinStrategy: IGoalJoinStrategy
{
    /// <inheritdoc/>
    public GoalJoinStrategyKey Key => GoalJoinStrategyKeys.BestEffort;

    /// <inheritdoc/>
    public ValueTask<GoalJoinDecision> EvaluateAsync(GoalJoinEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var open = JoinStrategySupport.Open(request.Children);
        if (!open.IsEmpty && !request.DeadlineElapsed)
        {
            return JoinStrategySupport.Done(new GoalJoinPending(open));
        }

        var eligible = JoinStrategySupport.Eligible(request.Children);
        return JoinStrategySupport.Done(new GoalJoinSatisfied(eligible, null, JoinStrategySupport.Cutoff(eligible)));
    }
}
