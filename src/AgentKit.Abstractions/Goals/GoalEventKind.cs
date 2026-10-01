// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the committed goal or delegation change a <see cref="GoalEvent"/> reports.</summary>
public enum GoalEventKind
{
    /// <summary>A goal was durably created.</summary>
    GoalCreated = 0,

    /// <summary>A goal changed status.</summary>
    GoalTransitioned = 1,

    /// <summary>An attempt started and took a goal's execution lease.</summary>
    AttemptStarted = 2,

    /// <summary>An attempt settled.</summary>
    AttemptSettled = 3,

    /// <summary>A child-admission intent was durably committed.</summary>
    DelegationDispatched = 4,

    /// <summary>A delegation was refused before any child was created.</summary>
    DelegationRejected = 5,

    /// <summary>A join reached a decision.</summary>
    JoinDecided = 6,
}
