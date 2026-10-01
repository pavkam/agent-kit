// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Carries one <c>AddGoalProfile</c> or <c>ReplaceGoalProfile</c> registration until the registry is built.</summary>
/// <param name="key">The profile key.</param>
/// <param name="configure">The configuration callback.</param>
/// <param name="replace">Whether the registration replaces earlier configuration instead of extending it.</param>
internal sealed class GoalProfileContributor(GoalProfileKey key, Action<GoalProfileOptions> configure, bool replace): IGoalProfileContributor
{
    /// <inheritdoc/>
    public GoalProfileKey Key { get; } = key;

    /// <inheritdoc/>
    public void Contribute(GoalProfileRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Configure(Key, configure, replace);
    }
}
