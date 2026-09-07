// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the current progress of one plan item.</summary>
public enum PlanItemStatus
{
    /// <summary>The item has not started.</summary>
    Pending,
    /// <summary>The item is the active unit of work.</summary>
    InProgress,
    /// <summary>The item finished successfully.</summary>
    Completed,
    /// <summary>The item cannot currently progress and needs an explicit unblock.</summary>
    Blocked,
}
