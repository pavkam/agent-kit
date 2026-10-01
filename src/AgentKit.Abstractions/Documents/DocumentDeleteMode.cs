// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects how far a document deletion goes.</summary>
public enum DocumentDeleteMode
{
    /// <summary>Commit an authoritative tombstone: every version becomes invisible immediately, while physical removal of content remains pending.</summary>
    Tombstone = 0,

    /// <summary>Tombstone if needed and then physically remove every version's chunks, leaving only content-free tombstone evidence.</summary>
    Purge = 1,
}
