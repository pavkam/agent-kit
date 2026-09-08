// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the bounded security transition represented by an audit record.</summary>
public enum SecurityAuditEventKind
{
    /// <summary>A normalized security request entered policy evaluation.</summary>
    Request,

    /// <summary>Policy evaluation produced an allow, denial, or approval requirement.</summary>
    Decision,

    /// <summary>An approval request was created or reached a terminal resolution.</summary>
    Approval,

    /// <summary>Grant evidence was issued or rejected before registration.</summary>
    GrantIssued,

    /// <summary>An effecting boundary recorded a required-audit accepted proposal before attempting grant consumption or protected access.</summary>
    EnforcementProposed,

    /// <summary>A grant store atomically recorded permission to begin one exact effect.</summary>
    GrantConsumptionIntent,

    /// <summary>A future grant use was revoked or expired.</summary>
    GrantLifecycle,

    /// <summary>An effecting boundary recorded a terminal effect certainty following a consumed grant.</summary>
    Enforcement,
}
