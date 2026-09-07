// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies an exact bounded file-snapshot observation.</summary>
public enum FileSnapshotStatus
{
    /// <summary>The complete file bytes and fingerprint were observed.</summary>
    Success,
    /// <summary>The authorized path did not exist.</summary>
    NotFound,
    /// <summary>Authorization or the no-follow boundary denied observation.</summary>
    Denied,
    /// <summary>The file exceeded the requested or host byte bound.</summary>
    LimitExceeded,
    /// <summary>The file changed while the snapshot was read.</summary>
    Changed,
    /// <summary>The host could not complete the observation.</summary>
    Failed,
}
