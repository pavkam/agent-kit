// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies why a delegation was rejected before any child was created.</summary>
public enum DelegationRejectionKind
{
    /// <summary>The request is malformed or names state that does not exist or is not active.</summary>
    InvalidRequest = 0,

    /// <summary>The security authority denied the delegation, or authority could not be selected or widened.</summary>
    Unauthorized = 1,

    /// <summary>No discoverable target satisfies the request.</summary>
    UnknownTarget = 2,

    /// <summary>More than one target satisfies the request and selection could not choose one.</summary>
    AmbiguousTarget = 3,

    /// <summary>A delegation policy denied the request.</summary>
    PolicyDenied = 4,

    /// <summary>A depth, child-count, or concurrency limit would be exceeded.</summary>
    LimitExceeded = 5,

    /// <summary>The requested budget could not be reserved.</summary>
    BudgetUnavailable = 6,

    /// <summary>The request's deadline had already passed.</summary>
    DeadlineElapsed = 7,

    /// <summary>The goal store or dispatcher needed for durable handoff was unavailable.</summary>
    HandoffUnavailable = 8,

    /// <summary>Required audit or event delivery was unavailable, so the operation failed closed.</summary>
    AuditUnavailable = 9,
}
