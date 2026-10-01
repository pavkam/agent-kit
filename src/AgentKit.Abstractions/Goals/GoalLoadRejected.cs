// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a load that the store refused or could not perform.</summary>
public sealed record GoalLoadRejected: GoalLoadResult
{
    /// <summary>Initializes a rejected result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public GoalLoadRejected(GoalStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the typed refusal.</summary>
    public GoalStoreFailure Failure { get; }
}
