// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the first-party join strategies.</summary>
/// <remarks>The all-results strategy is the default. Fastest-valid-success is the only timing-sensitive strategy and an explicit opt-in.</remarks>
public static class GoalJoinStrategyKeys
{
    /// <summary>Gets the key of the all-results join, which waits for every child and orders results by recorded ordinal.</summary>
    public static GoalJoinStrategyKey All { get; } = new("agentkit.join.all");

    /// <summary>Gets the key of the ordinal-first-success join, which waits until every earlier child is terminally ineligible before it selects a later success.</summary>
    public static GoalJoinStrategyKey OrdinalFirstSuccess { get; } = new("agentkit.join.ordinal-first-success");

    /// <summary>Gets the key of the fastest-valid-success join, which chooses the eligible child with the lowest durable settlement sequence.</summary>
    public static GoalJoinStrategyKey FastestValidSuccess { get; } = new("agentkit.join.fastest-valid-success");

    /// <summary>Gets the key of the quorum join, which is satisfied once enough children are eligible.</summary>
    public static GoalJoinStrategyKey Quorum { get; } = new("agentkit.join.quorum");

    /// <summary>Gets the key of the best-effort join, which yields the eligible children once every child settles or the wait cutoff is reached.</summary>
    public static GoalJoinStrategyKey BestEffort { get; } = new("agentkit.join.best-effort");
}
