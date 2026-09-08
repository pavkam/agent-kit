// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Names the finite terminal outcomes of one input-promotion planning operation.</summary>
internal enum InputPromotionPlanOutcome
{
    /// <summary>The policy produced an exact promotion plan.</summary>
    Planned,

    /// <summary>The policy rejected a plan that would exceed its required selection bound.</summary>
    Rejected,

    /// <summary>The caller cancelled planning before it produced a terminal plan.</summary>
    Cancelled,

    /// <summary>Planning ended with an unexpected semantic failure.</summary>
    Failed,
}
