// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the store a goal profile selected.</summary>
public sealed record GoalStoreSelected: GoalStoreSelectionResult
{
    /// <summary>Initializes a selected result.</summary>
    /// <param name="key">The registration key of the selected store.</param>
    /// <param name="store">The borrowed store instance; the selector retains ownership.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is null.</exception>
    public GoalStoreSelected(GoalStoreKey key, IGoalStore store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(store);
        Key = key;
        Store = store;
    }

    /// <summary>Gets the registration key of the selected store.</summary>
    public GoalStoreKey Key { get; }

    /// <summary>Gets the borrowed store instance.</summary>
    public IGoalStore Store { get; }
}
