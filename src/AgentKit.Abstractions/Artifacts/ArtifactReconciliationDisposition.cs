// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the terminal disposition reconciliation established for one reference-commit intent.</summary>
public enum ArtifactReconciliationDisposition
{
    /// <summary>The caller committed the reference; the artifact is retained and owned by that reference.</summary>
    ReferenceCommitted,
    /// <summary>A late commit was fenced out and the unreferenced staging or committed version was removed.</summary>
    Collected,
    /// <summary>The intent was already collected by an earlier reconciliation.</summary>
    AlreadyCollected,
}
