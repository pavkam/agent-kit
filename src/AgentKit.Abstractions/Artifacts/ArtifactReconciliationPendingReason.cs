// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Explains why reconciliation retained the artifact and left the intent unresolved.</summary>
public enum ArtifactReconciliationPendingReason
{
    /// <summary>The pin or orphan-retention window covering a possible late commit is still open.</summary>
    RetentionWindowOpen,
    /// <summary>The caller-owned intent evidence could not be established, so the object is retained conservatively.</summary>
    EvidenceUnavailable,
    /// <summary>Retention, legal hold, or external ownership prohibits collecting the object.</summary>
    RetentionHold,
}
