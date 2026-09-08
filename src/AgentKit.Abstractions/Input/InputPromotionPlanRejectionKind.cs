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
}
