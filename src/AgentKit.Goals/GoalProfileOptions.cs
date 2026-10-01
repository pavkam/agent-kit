// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Configures one named, versioned goal profile.</summary>
/// <remarks>The configured values are validated and frozen into an immutable <see cref="GoalProfileSnapshot"/> when the profile is published. Limits left null inherit the host ceilings from <see cref="AgentGoalOptions"/>; a value above a ceiling is refused.</remarks>
public sealed class GoalProfileOptions
{
    /// <summary>Gets or sets the published revision of this profile.</summary>
    /// <value>A positive revision. The default is one; publishing changed behavior under the same key needs a new version.</value>
    public GoalProfileVersion Version { get; set; } = new(1);

    /// <summary>Gets or sets the goal store this profile persists to.</summary>
    public GoalStoreKey StoreKey { get; set; }

    /// <summary>Gets or sets the dispatcher this profile hands children to.</summary>
    public DelegationDispatcherKey DispatcherKey { get; set; }

    /// <summary>Gets the delegation policies this profile evaluates, in addition to the baseline policy that is always first.</summary>
    /// <value>A mutable list of registered policy identities; empty adds nothing to the baseline.</value>
    public List<ComponentId> PolicyIds { get; } = [];

    /// <summary>Gets the join strategies parents may declare.</summary>
    /// <value>A mutable list that defaults to every first-party strategy.</value>
    public List<GoalJoinStrategyKey> JoinStrategies { get; } =
    [
        GoalJoinStrategyKeys.All,
        GoalJoinStrategyKeys.OrdinalFirstSuccess,
        GoalJoinStrategyKeys.FastestValidSuccess,
        GoalJoinStrategyKeys.Quorum,
        GoalJoinStrategyKeys.BestEffort,
    ];

    /// <summary>Gets or sets the join strategy used when a request names none, or <see langword="null"/> to inherit the host default.</summary>
    public GoalJoinStrategyKey? DefaultJoinStrategy { get; set; }

    /// <summary>Gets or sets the delegation depth ceiling, or <see langword="null"/> to inherit the host ceiling.</summary>
    public int? MaximumDelegationDepth { get; set; }

    /// <summary>Gets or sets the child-count ceiling per goal, or <see langword="null"/> to inherit the host ceiling.</summary>
    public int? MaximumChildrenPerGoal { get; set; }

    /// <summary>Gets or sets the concurrent-attempt ceiling, or <see langword="null"/> to inherit the host ceiling.</summary>
    public int? MaximumConcurrentAttempts { get; set; }

    /// <summary>Gets or sets the sibling-failure behavior, or <see langword="null"/> to inherit the host default.</summary>
    public DelegationFailureMode? FailureMode { get; set; }

    /// <summary>Gets or sets the ceiling for a run's implicit root goal, or <see langword="null"/> for one hundred turns, five hundred tool calls, and the profile's child ceiling.</summary>
    public GoalBudget? RootBudget { get; set; }
}
