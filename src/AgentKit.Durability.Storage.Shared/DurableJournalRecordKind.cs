// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Discriminates the transitions a durable journal appends to its record log.</summary>
/// <remarks>
/// The kind is persisted by stable name rather than ordinal, so adding a transition never reinterprets an existing
/// record. A replay that meets an unrecognized kind fails closed rather than skipping the line, because a skipped
/// transition would silently understate what a recovering worker already did.
/// </remarks>
internal enum DurableJournalRecordKind
{
    /// <summary>One acceptance record admitting an operation before any effect runs.</summary>
    Started,

    /// <summary>One progress snapshot replacing the operation's recorded state.</summary>
    Checkpointed,

    /// <summary>One authoritative terminal record settling the operation permanently.</summary>
    Settled,

    /// <summary>One wait record parking the operation on an external outcome.</summary>
    Waiting,

    /// <summary>One whole-operation state snapshot written only by compaction.</summary>
    /// <remarks>
    /// Compaction replaces a long transition history with the state it produced. This kind exists so a compacted log
    /// stays a valid append target: a snapshot fully determines the operation, and later transitions append after it.
    /// </remarks>
    Snapshot,
}
