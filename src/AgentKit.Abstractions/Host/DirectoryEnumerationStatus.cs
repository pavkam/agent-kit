// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies one terminal directory-enumeration attempt.</summary>
public enum DirectoryEnumerationStatus
{
    /// <summary>A deterministic page was observed, including an empty page.</summary>
    Success,
    /// <summary>The authorized target did not exist.</summary>
    NotFound,
    /// <summary>Authorization or a sandbox boundary denied observation.</summary>
    Denied,
    /// <summary>The configured total-snapshot bound was exceeded.</summary>
    LimitExceeded,
    /// <summary>The supplied continuation refers to a different directory snapshot.</summary>
    SnapshotChanged,
    /// <summary>The directory could not be enumerated for a non-sensitive host reason.</summary>
    Failed,
}
