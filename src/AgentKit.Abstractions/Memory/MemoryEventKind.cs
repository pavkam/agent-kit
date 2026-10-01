// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the bounded kinds of immutable memory and retrieval events.</summary>
public enum MemoryEventKind
{
    /// <summary>A proposal was accepted and made durable.</summary>
    ProposalAccepted = 0,

    /// <summary>A proposal was refused by policy.</summary>
    ProposalDenied = 1,

    /// <summary>A memory was corrected.</summary>
    MemoryCorrected = 2,

    /// <summary>A memory was logically deleted by a committed tombstone.</summary>
    MemoryDeleted = 3,

    /// <summary>A memory's body was physically purged.</summary>
    MemoryPurged = 4,

    /// <summary>A retrieval completed and its candidates were exposed to the caller.</summary>
    RetrievalCompleted = 5,

    /// <summary>A retrieval failed or was refused.</summary>
    RetrievalRejected = 6,

    /// <summary>A document version was published.</summary>
    DocumentPublished = 7,

    /// <summary>A document was deleted and its deletion propagated.</summary>
    DocumentDeleted = 8,
}
