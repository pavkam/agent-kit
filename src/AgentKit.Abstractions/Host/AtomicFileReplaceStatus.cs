// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies a version-conditional atomic file replacement.</summary>
public enum AtomicFileReplaceStatus
{
    /// <summary>The exact authorized bytes replaced the expected version atomically.</summary>
    Committed,
    /// <summary>The target did not exist at the effect gate.</summary>
    NotFound,
    /// <summary>The target bytes no longer matched the expected fingerprint.</summary>
    Conflict,
    /// <summary>Authorization or the no-follow boundary denied mutation.</summary>
    Denied,
    /// <summary>The host failed before committing the target replacement.</summary>
    Failed,
}
