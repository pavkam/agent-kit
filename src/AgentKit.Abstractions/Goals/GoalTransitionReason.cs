// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the bounded reason recorded with a goal transition.</summary>
public enum GoalTransitionReason
{
    /// <summary>The goal was admitted for execution.</summary>
    Admitted = 0,

    /// <summary>A delegation dispatcher committed a child-admission intent.</summary>
    DelegationDispatched = 1,

    /// <summary>An attempt started and took the execution lease.</summary>
    AttemptStarted = 2,

    /// <summary>The goal parked while children make progress.</summary>
    WaitingOnChildren = 3,

    /// <summary>The goal resumed after its wait was satisfied.</summary>
    Resumed = 4,

    /// <summary>The goal completed with a verified outcome reference.</summary>
    Completed = 5,

    /// <summary>The latest attempt failed.</summary>
    AttemptFailed = 6,

    /// <summary>A failed or blocked goal was made ready again; the next attempt is a new attempt.</summary>
    Retried = 7,

    /// <summary>The goal or its parent was cancelled.</summary>
    Cancelled = 8,

    /// <summary>Progress requires external authority or change.</summary>
    AuthorityRequired = 9,

    /// <summary>The goal's deadline elapsed before it settled.</summary>
    DeadlineExceeded = 10,

    /// <summary>A worker lost its execution lease or was lost with the process.</summary>
    LeaseLost = 11,
}
