// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the operation-level settlement of a workspace patch.</summary>
public enum WorkspacePatchStatus
{
    /// <summary>One entry committed with atomic target visibility and no subset was observable.</summary>
    AtomicCommitted,
    /// <summary>Every entry committed, but observers could see an intermediate subset.</summary>
    CommittedWithNonAtomicVisibility,
    /// <summary>A source-ordered prefix committed and later entries did not.</summary>
    Partial,
    /// <summary>Validation, authorization, or preconditions rejected the complete plan before target effects.</summary>
    RejectedBeforeEffect,
}
