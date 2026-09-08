// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies a failed atomic run-acceptance precondition without parsing text.</summary>
public enum SessionRunStartConflictKind
{
    /// <summary>The idempotency identity was reused with different evidence.</summary>
    Idempotency,
    /// <summary>The expected lane revision changed.</summary>
    LaneRevision,
    /// <summary>The session or branch version changed.</summary>
    SessionVersion,
    /// <summary>The selected branch tip changed.</summary>
    BranchCursor,
    /// <summary>At least one selected admission carries a different immutable identity from the authorized run.</summary>
    AdmissionIdentity,
    /// <summary>The cutoff or exact ordered admission plan is stale or invalid.</summary>
    PromotionPlan,
    /// <summary>The initiating admission carries a different before-run correlation from the proposed run.</summary>
    AdmissionCorrelation,
}
