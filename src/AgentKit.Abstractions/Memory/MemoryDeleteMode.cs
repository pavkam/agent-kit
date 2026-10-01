// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects how far a memory deletion goes.</summary>
public enum MemoryDeleteMode
{
    /// <summary>Commit an authoritative tombstone: the record becomes invisible immediately, while physical removal of its body remains pending.</summary>
    Tombstone = 0,

    /// <summary>Tombstone if needed and then physically remove the body, leaving only content-free tombstone evidence.</summary>
    Purge = 1,
}
