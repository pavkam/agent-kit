// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the durable lifecycle state of one goal.</summary>
/// <remarks>Every change between members is an explicit <see cref="GoalTransition"/>; <see cref="GoalTransition.IsValid"/> is the single validity table. Blocked means progress requires external authority or change, not that the work is merely difficult.</remarks>
public enum GoalStatus
{
    /// <summary>The goal was recorded but has not been admitted for execution.</summary>
    Proposed = 0,

    /// <summary>The goal is admitted and waits for an attempt to start. A delegated child in this state is a durable child-admission intent.</summary>
    Ready = 1,

    /// <summary>One attempt currently holds the goal's execution lease.</summary>
    Active = 2,

    /// <summary>The goal is parked on children or external input and holds no execution lease.</summary>
    Waiting = 3,

    /// <summary>The goal finished with a verified outcome reference. Terminal.</summary>
    Completed = 4,

    /// <summary>The goal's latest attempt failed. A retry moves it back to <see cref="Ready"/> and creates a new attempt.</summary>
    Failed = 5,

    /// <summary>The goal was cancelled. Completed effects remain and keep their reported certainty. Terminal.</summary>
    Cancelled = 6,

    /// <summary>Progress requires external authority or change.</summary>
    Blocked = 7,
}
