// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a selector to bind the store named by a captured goal profile.</summary>
public sealed record GoalStoreSelectionRequest
{
    /// <summary>Initializes a validated selection request.</summary>
    /// <param name="profile">The captured profile whose store is wanted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    public GoalStoreSelectionRequest(GoalProfileReference profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Profile = profile;
    }

    /// <summary>Gets the captured profile reference.</summary>
    public GoalProfileReference Profile { get; }
}
