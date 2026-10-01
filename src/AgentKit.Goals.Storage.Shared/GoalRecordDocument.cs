// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Is the persisted form of a <see cref="GoalRecord"/>, excluding any delegation's captured authorization.</summary>
/// <remarks>Adapters that persist delegation evidence wrap this document with their own delegation payload; the session-backed projection persists the goal, attempts, and transitions only.</remarks>
/// <param name="Goal">The goal state.</param>
/// <param name="Attempts">The attempts in start order.</param>
/// <param name="Transitions">The transitions in commit order.</param>
/// <param name="Sequence">The store-wide creation sequence.</param>
/// <param name="ChildOrdinal">The child ordinal, or <see langword="null"/>.</param>
/// <param name="SettledSequence">The settlement sequence, or <see langword="null"/>.</param>
internal sealed record GoalRecordDocument(
    GoalDocument Goal,
    ImmutableArray<GoalAttemptDocument> Attempts,
    ImmutableArray<GoalTransitionDocument> Transitions,
    long Sequence,
    int? ChildOrdinal,
    long? SettledSequence)
{
    /// <summary>Converts an aggregate to its persisted form.</summary>
    /// <param name="value">The non-null aggregate.</param>
    /// <returns>The document, without delegation.</returns>
    internal static GoalRecordDocument FromDomain(GoalRecord value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            GoalDocument.FromDomain(value.Goal),
            [.. value.Attempts.Select(GoalAttemptDocument.FromDomain)],
            [.. value.Transitions.Select(GoalTransitionDocument.FromDomain)],
            value.Sequence,
            value.ChildOrdinal,
            value.SettledSequence);
    }

    /// <summary>Restores the aggregate, re-running every validation.</summary>
    /// <param name="delegation">The delegation restored by the adapter, or <see langword="null"/>.</param>
    /// <returns>The aggregate.</returns>
    internal GoalRecord ToDomain(DelegationRequest? delegation)
    {
        ArgumentNullException.ThrowIfNull(Goal);
        var attempts = Attempts.IsDefault ? [] : Attempts;
        var transitions = Transitions.IsDefault ? [] : Transitions;
        ArgumentException.ThrowIfContainsNull(attempts, nameof(Attempts));
        ArgumentException.ThrowIfContainsNull(transitions, nameof(Transitions));
        return new(
            Goal.ToDomain(),
            [.. attempts.Select(static item => item.ToDomain())],
            [.. transitions.Select(static item => item.ToDomain())],
            delegation,
            Sequence,
            ChildOrdinal,
            SettledSequence);
    }
}
