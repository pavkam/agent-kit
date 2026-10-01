// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Starts a new attempt as the goal becomes <see cref="GoalStatus.Active"/>.</summary>
public sealed record GoalAttemptStart: GoalAttemptChange
{
    /// <summary>Initializes a start change.</summary>
    /// <param name="attempt">The new running attempt, numbered one past the last recorded attempt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="attempt"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="attempt"/> is not <see cref="GoalAttemptStatus.Running"/>.</exception>
    public GoalAttemptStart(GoalAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentException.ThrowIfNotEqual(attempt.Status, GoalAttemptStatus.Running, nameof(attempt));
        Attempt = attempt;
    }

    /// <summary>Gets the new running attempt.</summary>
    public GoalAttempt Attempt { get; }
}
