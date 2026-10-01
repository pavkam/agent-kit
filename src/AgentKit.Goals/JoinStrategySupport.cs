// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Holds the small pure helpers every first-party join strategy shares.</summary>
/// <remarks>None of these helpers, and no strategy, reads task completion order or a clock: decisions derive only from recorded child ordinals, durable statuses, eligibility, and durable settlement sequences.</remarks>
internal static class JoinStrategySupport
{
    /// <summary>Lists the children that can still change, in ordinal order.</summary>
    /// <param name="children">The parent's children in ordinal order.</param>
    /// <returns>The open children's goal identities.</returns>
    internal static ImmutableArray<GoalId> Open(ImmutableArray<GoalJoinChild> children) =>
        [.. children.Where(static child => child.IsOpen).Select(static child => child.ChildGoalId)];

    /// <summary>Lists the eligible children in ordinal order.</summary>
    /// <param name="children">The parent's children in ordinal order.</param>
    /// <returns>The eligible children.</returns>
    internal static ImmutableArray<GoalJoinChild> Eligible(ImmutableArray<GoalJoinChild> children) =>
        [.. children.Where(static child => child.Eligible)];

    /// <summary>Returns the highest durable settlement sequence among children, or zero.</summary>
    /// <param name="children">The children considered.</param>
    /// <returns>The highest sequence, or zero when none has settled.</returns>
    internal static long Cutoff(IEnumerable<GoalJoinChild> children) => children.Max(static child => child.SettledSequence) ?? 0;

    /// <summary>Wraps a synchronously computed decision.</summary>
    /// <param name="decision">The decision.</param>
    /// <returns>A completed value task.</returns>
    internal static ValueTask<GoalJoinDecision> Done(GoalJoinDecision decision) => ValueTask.FromResult(decision);
}
