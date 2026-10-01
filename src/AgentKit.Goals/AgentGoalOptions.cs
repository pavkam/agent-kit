// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Sets the engine-wide hard ceilings and defaults every goal profile inherits.</summary>
/// <remarks>Host options are ceilings. A profile or request may reserve less depth, fewer children, fewer concurrent attempts, or a smaller budget, but never more; the profile registry and the delegation policy enforce that.</remarks>
public sealed class AgentGoalOptions
{
    /// <summary>Gets or sets the deepest delegation chain allowed.</summary>
    /// <value>A positive depth. A root goal has depth zero, so the default of four allows four levels of delegation.</value>
    public int MaximumDelegationDepth { get; set; } = 4;

    /// <summary>Gets or sets the most children any one goal may have.</summary>
    /// <value>A positive count. The default is eight.</value>
    public int MaximumChildrenPerGoal { get; set; } = 8;

    /// <summary>Gets or sets the most attempts that may run concurrently under one profile.</summary>
    /// <value>A positive count. The default is four.</value>
    public int MaximumConcurrentAttempts { get; set; } = 4;

    /// <summary>Gets or sets the join strategy used when a profile and request name none.</summary>
    /// <value>The all-results strategy by default.</value>
    public GoalJoinStrategyKey DefaultJoinStrategy { get; set; } = GoalJoinStrategyKeys.All;

    /// <summary>Gets or sets how one child's failure affects its siblings.</summary>
    /// <value>Every sibling settles by default.</value>
    public DelegationFailureMode FailureMode { get; set; } = DelegationFailureMode.SettleAllChildren;

    /// <summary>Gets or sets how often a waiting parent re-reads its children's durable state.</summary>
    /// <value>A positive interval measured on the injected clock. The default is 250 milliseconds.</value>
    public TimeSpan JoinPollInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Gets or sets how long a goal-store or delegation grant stays valid after issue.</summary>
    /// <value>A positive duration. The default is one minute.</value>
    public TimeSpan GrantLifetime { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the largest child-result summary, in characters, the parent accepts.</summary>
    /// <value>A positive bound. The default is 16,000.</value>
    public int MaximumResultSummaryCharacters { get; set; } = 16_000;
}
