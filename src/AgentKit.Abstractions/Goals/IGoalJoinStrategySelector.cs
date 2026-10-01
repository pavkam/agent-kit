// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves a declared join strategy key to a registered strategy, validated against the captured profile.</summary>
public interface IGoalJoinStrategySelector
{
    /// <summary>Selects the declared strategy.</summary>
    /// <param name="request">The captured profile and declared key.</param>
    /// <param name="cancellationToken">Cancels the selection.</param>
    /// <returns>The strategy, or a rejection when the key is unknown or the profile does not allow it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<GoalJoinStrategySelectionResult> SelectAsync(
        GoalJoinStrategySelectionRequest request,
        CancellationToken cancellationToken = default);
}
