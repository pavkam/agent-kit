// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports an applied transition, either newly applied or returned by an idempotent replay.</summary>
public sealed record GoalTransitioned: GoalTransitionResult
{
    /// <summary>Initializes an applied result.</summary>
    /// <param name="record">The aggregate after the transition.</param>
    /// <param name="replayed"><see langword="true"/> when an earlier equivalent request had already applied the transition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    public GoalTransitioned(GoalRecord record, bool replayed)
    {
        ArgumentNullException.ThrowIfNull(record);
        Record = record;
        Replayed = replayed;
    }

    /// <summary>Gets the aggregate after the transition.</summary>
    public GoalRecord Record { get; }

    /// <summary>Gets a value indicating whether the result is an idempotent replay.</summary>
    public bool Replayed { get; }
}
