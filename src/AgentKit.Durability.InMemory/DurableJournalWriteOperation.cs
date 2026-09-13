// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Defines the bounded write-stage dimensions emitted by durable-journal diagnostics.</summary>
internal enum DurableJournalWriteOperation
{
    /// <summary>Committing an operation's acceptance record.</summary>
    RecordStart,

    /// <summary>Committing a complete state snapshot at a semantic boundary.</summary>
    RecordCheckpoint,

    /// <summary>Committing the operation's authoritative terminal record.</summary>
    RecordTerminal,
}
