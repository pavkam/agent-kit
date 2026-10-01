// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a loaded goal aggregate.</summary>
public sealed record GoalLoaded: GoalLoadResult
{
    /// <summary>Initializes a loaded result.</summary>
    /// <param name="record">The durable aggregate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    public GoalLoaded(GoalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Record = record;
    }

    /// <summary>Gets the durable aggregate.</summary>
    public GoalRecord Record { get; }
}
