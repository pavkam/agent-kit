// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Names the finite terminal outcomes of one durable-journal write.</summary>
internal enum DurableJournalWriteOutcome
{
    /// <summary>The write committed durably under the presented ownership generation.</summary>
    Recorded,

    /// <summary>The presented ownership generation is older than the current authoritative one.</summary>
    Fenced,

    /// <summary>The write could not commit for a reason other than fencing.</summary>
    Failed,

    /// <summary>The caller cancelled the attempt before a terminal outcome.</summary>
    Cancelled,
}
