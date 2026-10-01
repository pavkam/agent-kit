// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The abstract outcome of reading a page of goals.</summary>
public abstract record GoalPageResult
{
    /// <summary>Restricts derivation to the contracts package.</summary>
    private protected GoalPageResult()
    {
    }
}
