// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Waits for every child, then yields all of them in recorded ordinal order, including children that did not succeed.</summary>
/// <remarks>This is the default join. Ordering is the recorded delegation ordinal, never completion order, so the decision is identical however the children happen to finish. The strategy is a pure function of its evaluation request and is thread-safe.</remarks>
internal sealed class AllResultsJoinStrategy: IGoalJoinStrategy
{
    /// <inheritdoc/>
    public GoalJoinStrategyKey Key => GoalJoinStrategyKeys.All;

    /// <inheritdoc/>
    public ValueTask<GoalJoinDecision> EvaluateAsync(GoalJoinEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var open = JoinStrategySupport.Open(request.Children);
        return JoinStrategySupport.Done(open.IsEmpty
            ? new GoalJoinSatisfied(request.Children, null, JoinStrategySupport.Cutoff(request.Children))
            : new GoalJoinPending(open));
    }
}
