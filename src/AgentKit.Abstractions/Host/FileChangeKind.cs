// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one observed file-system change event.</summary>
public enum FileChangeKind
{
    /// <summary>A file or directory was created.</summary>
    Created = 0,

    /// <summary>An existing entry was modified.</summary>
    Modified = 1,

    /// <summary>An entry was deleted.</summary>
    Deleted = 2,

    /// <summary>An entry was renamed; the path names the post-rename location when known.</summary>
    Renamed = 3,

    /// <summary>Observation overflowed a bounded buffer and may have dropped events.</summary>
    Overflow = 4,
}
