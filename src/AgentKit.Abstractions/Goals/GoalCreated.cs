// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a durably created goal, either newly created or returned by an idempotent replay.</summary>
public sealed record GoalCreated: GoalCreateResult
{
    /// <summary>Initializes a created result.</summary>
    /// <param name="record">The durable aggregate.</param>
    /// <param name="replayed"><see langword="true"/> when an earlier equivalent request had already created the goal.</param>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    public GoalCreated(GoalRecord record, bool replayed)
    {
        ArgumentNullException.ThrowIfNull(record);
        Record = record;
        Replayed = replayed;
    }

    /// <summary>Gets the durable aggregate.</summary>
    public GoalRecord Record { get; }

    /// <summary>Gets a value indicating whether the result is an idempotent replay.</summary>
    public bool Replayed { get; }
}
