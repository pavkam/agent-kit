// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Names the terminal run outcome a case expects.</summary>
/// <remarks>The values mirror the semantic outcome family of a finished run so refusal, policy halt, limit exhaustion, and failure stay distinguishable from success.</remarks>
public enum ExpectedRunOutcome
{
    /// <summary>The run succeeded.</summary>
    Succeeded = 0,

    /// <summary>The run went idle without a result.</summary>
    Idle = 1,

    /// <summary>The run deferred to an external continuation.</summary>
    Deferred = 2,

    /// <summary>The run was cancelled.</summary>
    Cancelled = 3,

    /// <summary>A budget limit stopped the run.</summary>
    LimitReached = 4,

    /// <summary>A policy halted the run.</summary>
    PolicyHalted = 5,

    /// <summary>The run failed.</summary>
    Failed = 6,
}
