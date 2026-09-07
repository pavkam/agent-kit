// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the terminal settlement of one delegated child attempt.</summary>
public enum TaskDelegationStatus
{
    /// <summary>The child produced a validated successful result.</summary>
    Succeeded,
    /// <summary>The child settled without satisfying its acceptance criteria.</summary>
    Failed,
    /// <summary>The child was cancelled and all admitted work settled.</summary>
    Cancelled,
    /// <summary>The child requires external authority or state change.</summary>
    Blocked,
}
