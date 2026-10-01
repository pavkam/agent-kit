// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Selects the earliest child by recorded ordinal that succeeded, waiting until every earlier child is terminally ineligible.</summary>
/// <remarks>A later success never wins while an earlier child can still succeed, so the winner is independent of completion timing and replays identically. With no child, or only ineligible ones, the join is unsatisfiable. The strategy is pure and thread-safe.</remarks>
internal sealed class OrdinalFirstSuccessJoinStrategy: IGoalJoinStrategy
{
    /// <inheritdoc/>
    public GoalJoinStrategyKey Key => GoalJoinStrategyKeys.OrdinalFirstSuccess;

    /// <inheritdoc/>
    public ValueTask<GoalJoinDecision> EvaluateAsync(GoalJoinEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var child in request.Children)
        {
            if (child.IsOpen)
            {
                return JoinStrategySupport.Done(new GoalJoinPending([child.ChildGoalId]));
            }

            if (child.Eligible)
            {
                return JoinStrategySupport.Done(new GoalJoinSatisfied([child], child.ChildGoalId, child.SettledSequence ?? 0));
            }
        }

        return JoinStrategySupport.Done(new GoalJoinUnsatisfiable("No child produced an eligible result."));
    }
}
