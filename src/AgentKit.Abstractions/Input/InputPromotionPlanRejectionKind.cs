// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies failures to form a complete deterministic promotion plan.</summary>
public enum InputPromotionPlanRejectionKind
{
    /// <summary>The configured bound cannot include every required eligible input.</summary>
    SelectionLimitExceeded,
    /// <summary>The eligible snapshot contains input for another address, lane, or future cutoff.</summary>
    InconsistentEligibleInput,
    /// <summary>
    /// No eligible input qualifies for promotion at this boundary (for example, only follow-up input is
    /// queued at a steering boundary, or nothing is queued at all). This is the ordinary "nothing to
    /// promote" outcome, not an error: the operation simply continues without promoting input.
    /// </summary>
    NothingEligible,
}
