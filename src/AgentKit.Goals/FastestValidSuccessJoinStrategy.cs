// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Selects the eligible child that settled first in the durable join-inbox sequence.</summary>
/// <remarks>
/// This strategy is explicitly timing-sensitive in live execution: whichever valid result is committed first wins. The winner
/// is still recoverable state, because the settlement sequence is durable and monotonic, so a later child can never displace
/// it and replaying the same durable state reproduces the same winner exactly. The strategy is pure and thread-safe.
/// </remarks>
internal sealed class FastestValidSuccessJoinStrategy: IGoalJoinStrategy
{
    /// <inheritdoc/>
    public GoalJoinStrategyKey Key => GoalJoinStrategyKeys.FastestValidSuccess;

    /// <inheritdoc/>
    public ValueTask<GoalJoinDecision> EvaluateAsync(GoalJoinEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var eligible = JoinStrategySupport.Eligible(request.Children);
        if (!eligible.IsEmpty)
        {
            var winner = eligible.MinBy(static child => child.SettledSequence)!;
            return JoinStrategySupport.Done(new GoalJoinSatisfied([winner], winner.ChildGoalId, winner.SettledSequence ?? 0));
        }

        var open = JoinStrategySupport.Open(request.Children);
        return JoinStrategySupport.Done(open.IsEmpty
            ? new GoalJoinUnsatisfiable("No child produced an eligible result.")
            : new GoalJoinPending(open));
    }
}
