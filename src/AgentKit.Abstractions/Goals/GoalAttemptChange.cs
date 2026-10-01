// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The abstract attempt mutation applied atomically with one goal transition.</summary>
/// <remarks>Attempt changes ride on transitions so a goal's status and its lease-holding attempt can never diverge.</remarks>
public abstract record GoalAttemptChange
{
    /// <summary>Restricts derivation to the contracts package.</summary>
    private protected GoalAttemptChange()
    {
    }
}
