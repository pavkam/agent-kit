// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the conditional, versioned states of one caller-owned reference-commit intent.</summary>
/// <remarks>Only <see cref="Pending"/> to <see cref="Committed"/>, <see cref="Pending"/> to <see cref="Fenced"/>, and <see cref="Fenced"/> to <see cref="Collected"/> are legal, so a late commit and collection race through one conditional transition and have one winner.</remarks>
public enum ArtifactReferenceCommitState
{
    /// <summary>The intent was recorded before finalization and the reference is not yet committed; a pin retains the object.</summary>
    Pending,
    /// <summary>The caller committed the reference in its owning store and completed the intent; terminal.</summary>
    Committed,
    /// <summary>Reconciliation fenced out any late commit; the object is now eligible for collection.</summary>
    Fenced,
    /// <summary>The fenced object was collected; terminal.</summary>
    Collected,
}
