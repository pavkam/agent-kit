// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Gives the planner read access to stored goals, however an adapter keeps them.</summary>
/// <remarks>An in-memory adapter answers from dictionaries and a SQL adapter from queries inside its transaction. The planner calls these members only while the adapter holds whatever gate or transaction makes the answers consistent with the write that follows.</remarks>
internal interface IGoalLookup
{
    /// <summary>Gets the creation sequence the next new goal receives.</summary>
    /// <value>One greater than the highest stored creation sequence.</value>
    public long NextSequence { get; }

    /// <summary>Gets the settlement sequence the next settling transition receives.</summary>
    /// <value>One greater than the highest stored settlement sequence.</value>
    public long NextSettledSequence { get; }

    /// <summary>Finds a goal by its creation idempotency key.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="key">The creation key.</param>
    /// <returns>The stored aggregate, or <see langword="null"/> when the key is unused.</returns>
    public GoalRecord? FindByCreationKey(TenantId tenant, string key);

    /// <summary>Finds a goal by identity.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="goalId">The goal identity.</param>
    /// <returns>The stored aggregate, or <see langword="null"/> when the tenant has no such goal.</returns>
    public GoalRecord? Find(TenantId tenant, GoalId goalId);

    /// <summary>Counts the children a parent already has.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="parentId">The parent identity.</param>
    /// <returns>The non-negative child count.</returns>
    public int CountChildren(TenantId tenant, GoalId parentId);
}
