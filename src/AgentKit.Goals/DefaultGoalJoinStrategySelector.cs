// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Resolves a declared join strategy key to a registered strategy the captured profile allows.</summary>
/// <param name="services">The root provider keyed strategies are resolved from.</param>
/// <param name="profiles">The profile catalog resolving captured references.</param>
internal sealed class DefaultGoalJoinStrategySelector(IServiceProvider services, IGoalProfileCatalog profiles): IGoalJoinStrategySelector
{
    /// <inheritdoc/>
    public ValueTask<GoalJoinStrategySelectionResult> SelectAsync(GoalJoinStrategySelectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!profiles.TryGet(request.Profile, out var profile))
        {
            return ValueTask.FromResult<GoalJoinStrategySelectionResult>(
                new GoalJoinStrategySelectionRejected("No goal profile is published for the captured key and version."));
        }

        if (!profile.JoinStrategies.Contains(request.StrategyKey))
        {
            return ValueTask.FromResult<GoalJoinStrategySelectionResult>(
                new GoalJoinStrategySelectionRejected("The goal profile does not allow the declared join strategy."));
        }

        var strategy = services.GetKeyedService<IGoalJoinStrategy>(request.StrategyKey.Value);
        return ValueTask.FromResult<GoalJoinStrategySelectionResult>(strategy is null
            ? new GoalJoinStrategySelectionRejected("No join strategy is registered for the declared key.")
            : new GoalJoinStrategySelected(strategy));
    }
}
