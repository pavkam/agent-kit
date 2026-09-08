// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the safe terminal fact recorded for one security audit event.</summary>
public enum SecurityAuditOutcome
{
    /// <summary>The audited transition was accepted or completed with known success.</summary>
    Accepted,

    /// <summary>The audited transition was rejected before a protected effect.</summary>
    Denied,

    /// <summary>The audited transition was cancelled before its authoritative commitment.</summary>
    Cancelled,

    /// <summary>The audited transition failed without proving a protected effect completed.</summary>
    Failed,

    /// <summary>The effect certainty remains unknown and requires reconciliation.</summary>
    Unknown,
}
