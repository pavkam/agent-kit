// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries a parent's join request with the durable child snapshot a strategy decides over.</summary>
public sealed record GoalJoinEvaluationRequest
{
    /// <summary>Initializes a validated evaluation request.</summary>
    /// <param name="join">The caller's join request.</param>
    /// <param name="children">The parent's children in recorded ordinal order; empty when none.</param>
    /// <param name="deadlineElapsed"><see langword="true"/> when the request's wait cutoff has been reached.</param>
    /// <exception cref="ArgumentNullException"><paramref name="join"/> or an element is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="children"/> is default or not in strictly increasing ordinal order.</exception>
    public GoalJoinEvaluationRequest(GoalJoinRequest join, ImmutableArray<GoalJoinChild> children, bool deadlineElapsed)
    {
        ArgumentNullException.ThrowIfNull(join);
        ArgumentException.ThrowIfDefault(children);
        ArgumentException.ThrowIfContainsNull(children);
        for (var index = 1; index < children.Length; index++)
        {
            ArgumentException.ThrowIfNotEqual(children[index].Ordinal > children[index - 1].Ordinal, true, nameof(children));
        }

        Join = join;
        Children = children;
        DeadlineElapsed = deadlineElapsed;
    }

    /// <summary>Gets the caller's join request.</summary>
    public GoalJoinRequest Join { get; }

    /// <summary>Gets the parent's children in recorded ordinal order.</summary>
    public ImmutableArray<GoalJoinChild> Children { get; }

    /// <summary>Gets a value indicating whether the wait cutoff has been reached.</summary>
    public bool DeadlineElapsed { get; }

    /// <inheritdoc/>
    public bool Equals(GoalJoinEvaluationRequest? other) =>
        other is not null && Join == other.Join && DeadlineElapsed == other.DeadlineElapsed && Children.SequenceEqual(other.Children);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Join);
        hash.Add(DeadlineElapsed);
        foreach (var child in Children)
        {
            hash.Add(child);
        }

        return hash.ToHashCode();
    }
}
