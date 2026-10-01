// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

using System.Diagnostics.CodeAnalysis;

/// <summary>Publishes the registry's profile snapshots through the neutral catalog contract.</summary>
/// <param name="registry">The non-null registry holding configuration.</param>
internal sealed class InMemoryGoalProfileCatalog(GoalProfileRegistry registry): IGoalProfileCatalog
{
    /// <inheritdoc/>
    public bool TryGet(GoalProfileKey key, [NotNullWhen(true)] out GoalProfileSnapshot? profile) => registry.TryGet(key, out profile);

    /// <inheritdoc/>
    public bool TryGet(GoalProfileReference reference, [NotNullWhen(true)] out GoalProfileSnapshot? profile)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (registry.TryGet(reference.Key, out var published) && published.Version == reference.Version)
        {
            profile = published;
            return true;
        }

        profile = null;
        return false;
    }
}
