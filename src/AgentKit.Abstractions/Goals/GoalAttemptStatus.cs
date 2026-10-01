// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the state of one execution attempt against a goal.</summary>
public enum GoalAttemptStatus
{
    /// <summary>The attempt holds the goal's execution lease and its run has not settled.</summary>
    Running = 0,

    /// <summary>The attempt ended with a verified successful outcome.</summary>
    Succeeded = 1,

    /// <summary>The attempt ended without success. The evidence is kept and a retry creates a new attempt.</summary>
    Failed = 2,

    /// <summary>The attempt was cancelled; already-performed effects remain and are reported with their certainty.</summary>
    Cancelled = 3,

    /// <summary>The attempt stopped because progress requires external authority or change.</summary>
    Blocked = 4,
}
