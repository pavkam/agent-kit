// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is the complete durable state of one goal: the goal, its attempts, its transitions, and its delegation.</summary>
/// <remarks>
/// <para>
/// A goal's status, ownership, and attempts are reconstructible from the recorded transitions; a store returns this
/// aggregate so a caller never sees a goal without the evidence that produced it. <see cref="Sequence"/> is the store-wide
/// creation order used for deterministic intent discovery, <see cref="ChildOrdinal"/> is the one-based position among
/// the parent's children (the default join order), and <see cref="SettledSequence"/> is the store-wide position at which
/// the goal last reached <see cref="GoalStatus.Completed"/>, <see cref="GoalStatus.Failed"/>, or
/// <see cref="GoalStatus.Cancelled"/> - the durable parent join-inbox order that timing-sensitive joins record and replay.
/// </para>
/// <para>The record is immutable. Transitions and attempts are append-only evidence; <see cref="Delegation"/> is present only on delegated child goals.</para>
/// </remarks>
public sealed record GoalRecord
{
    /// <summary>Initializes a validated aggregate.</summary>
    /// <param name="goal">The current goal state.</param>
    /// <param name="attempts">The attempts in start order; empty when none started.</param>
    /// <param name="transitions">The transitions in commit order; empty for a freshly created goal.</param>
    /// <param name="delegation">The delegation that created the goal, or <see langword="null"/>.</param>
    /// <param name="sequence">The positive store-wide creation sequence.</param>
    /// <param name="childOrdinal">The one-based position among the parent's children, or <see langword="null"/> for a root goal.</param>
    /// <param name="settledSequence">The positive store-wide settlement sequence, or <see langword="null"/> when not settled.</param>
    /// <exception cref="ArgumentNullException"><paramref name="goal"/> or an element is null.</exception>
    /// <exception cref="ArgumentException">An array is default, or an attempt or transition names another goal, or a child ordinal is present without a parent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A sequence or ordinal is not positive.</exception>
    public GoalRecord(
        AgentGoal goal,
        ImmutableArray<GoalAttempt> attempts,
        ImmutableArray<GoalTransition> transitions,
        DelegationRequest? delegation,
        long sequence,
        int? childOrdinal,
        long? settledSequence)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentException.ThrowIfDefault(attempts);
        ArgumentException.ThrowIfDefault(transitions);
        ArgumentException.ThrowIfContainsNull(attempts);
        ArgumentException.ThrowIfContainsNull(transitions);
        foreach (var attempt in attempts)
        {
            ArgumentException.ThrowIfNotEqual(attempt.GoalId, goal.Id, nameof(attempts));
        }

        foreach (var transition in transitions)
        {
            ArgumentException.ThrowIfNotEqual(transition.GoalId, goal.Id, nameof(transitions));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        if (childOrdinal is { } ordinal)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ordinal, nameof(childOrdinal));
            ArgumentException.ThrowIfNotEqual(goal.ParentId is null, false, nameof(childOrdinal));
        }

        if (settledSequence is { } settled)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settled, nameof(settledSequence));
        }

        Goal = goal;
        Attempts = attempts;
        Transitions = transitions;
        Delegation = delegation;
        Sequence = sequence;
        ChildOrdinal = childOrdinal;
        SettledSequence = settledSequence;
    }

    /// <summary>Gets the current goal state.</summary>
    public AgentGoal Goal { get; }

    /// <summary>Gets the attempts in start order.</summary>
    public ImmutableArray<GoalAttempt> Attempts { get; }

    /// <summary>Gets the transitions in commit order.</summary>
    public ImmutableArray<GoalTransition> Transitions { get; }

    /// <summary>Gets the delegation that created the goal, or <see langword="null"/> for a goal that was not delegated.</summary>
    public DelegationRequest? Delegation { get; }

    /// <summary>Gets the store-wide creation sequence.</summary>
    public long Sequence { get; }

    /// <summary>Gets the one-based position among the parent's children, or <see langword="null"/> for a root goal.</summary>
    public int? ChildOrdinal { get; }

    /// <summary>Gets the store-wide settlement sequence, or <see langword="null"/> when the goal has not settled.</summary>
    public long? SettledSequence { get; }

    /// <summary>Gets the running attempt that holds the goal's execution lease, or <see langword="null"/>.</summary>
    public GoalAttempt? ActiveAttempt =>
        Goal.ActiveAttemptId is { } id ? Attempts.FirstOrDefault(attempt => attempt.Id == id) : null;

    /// <inheritdoc/>
    public bool Equals(GoalRecord? other) =>
        other is not null
        && Goal == other.Goal
        && Attempts.SequenceEqual(other.Attempts)
        && Transitions.SequenceEqual(other.Transitions)
        && Delegation == other.Delegation
        && Sequence == other.Sequence
        && ChildOrdinal == other.ChildOrdinal
        && SettledSequence == other.SettledSequence;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Goal);
        foreach (var attempt in Attempts)
        {
            hash.Add(attempt);
        }

        foreach (var transition in Transitions)
        {
            hash.Add(transition);
        }

        hash.Add(Delegation);
        hash.Add(Sequence);
        hash.Add(ChildOrdinal);
        hash.Add(SettledSequence);
        return hash.ToHashCode();
    }
}
