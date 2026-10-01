// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a started attempt.</summary>
public sealed record GoalAttemptStarted: GoalAttemptResult
{
    /// <summary>Initializes a started result.</summary>
    /// <param name="record">The aggregate after the attempt started.</param>
    /// <param name="attempt">The started attempt, which is the record's active attempt.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    public GoalAttemptStarted(GoalRecord record, GoalAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(attempt);
        Record = record;
        Attempt = attempt;
    }

    /// <summary>Gets the aggregate after the attempt started.</summary>
    public GoalRecord Record { get; }

    /// <summary>Gets the started attempt.</summary>
    public GoalAttempt Attempt { get; }
}
