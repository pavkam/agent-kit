// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies why unpublished staging content is being discarded.</summary>
public enum ArtifactAbortReason
{
    /// <summary>The caller cancelled before publication.</summary>
    Cancelled,
    /// <summary>Integrity validation failed.</summary>
    IntegrityFailure,
    /// <summary>Reference commitment failed and compensation is required.</summary>
    ReferenceCommitFailure,
    /// <summary>The staging receipt expired.</summary>
    Expired,
    /// <summary>The caller explicitly abandoned the preparation.</summary>
    Abandoned,
}
