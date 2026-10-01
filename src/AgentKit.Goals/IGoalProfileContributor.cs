// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Applies one registered profile configuration to the registry when the catalog is first built.</summary>
internal interface IGoalProfileContributor
{
    /// <summary>Gets the profile this contributor configures.</summary>
    public GoalProfileKey Key { get; }

    /// <summary>Applies the configuration.</summary>
    /// <param name="registry">The non-null registry to configure.</param>
    public void Contribute(GoalProfileRegistry registry);
}
