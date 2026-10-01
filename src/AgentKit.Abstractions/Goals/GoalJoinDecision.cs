// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The abstract decision of a join strategy over a parent's durable children.</summary>
public abstract record GoalJoinDecision
{
    /// <summary>Restricts derivation to the contracts package.</summary>
    private protected GoalJoinDecision()
    {
    }
}
