// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The abstract outcome of applying a goal transition.</summary>
public abstract record GoalTransitionResult
{
    /// <summary>Restricts derivation to the contracts package.</summary>
    private protected GoalTransitionResult()
    {
    }
}
