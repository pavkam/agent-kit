// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies stable artifact lifecycle failures.</summary>
public enum ArtifactFailureKind
{
    /// <summary>Security policy denied the operation.</summary>
    Denied,
    /// <summary>Content exceeded the configured byte limit.</summary>
    LimitExceeded,
    /// <summary>Declared and observed length or integrity did not match.</summary>
    IntegrityMismatch,
    /// <summary>The preparation or committed artifact was not found.</summary>
    NotFound,
    /// <summary>An idempotency key was reused with different intent.</summary>
    Conflict,
    /// <summary>Retention or legal hold prevents the operation.</summary>
    RetentionConflict,
    /// <summary>The selected backend was unavailable.</summary>
    Unavailable,
    /// <summary>The operation failed safely for another reason.</summary>
    Failed,
}
