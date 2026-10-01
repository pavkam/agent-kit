// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a page read that the store refused or could not perform.</summary>
public sealed record GoalPageRejected: GoalPageResult
{
    /// <summary>Initializes a rejected result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public GoalPageRejected(GoalStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the typed refusal.</summary>
    public GoalStoreFailure Failure { get; }
}
