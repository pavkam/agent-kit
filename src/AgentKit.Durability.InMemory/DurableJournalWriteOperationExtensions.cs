// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Maps finite durable-journal write operations to stable diagnostic values.</summary>
internal static class DurableJournalWriteOperationExtensions
{
    extension(DurableJournalWriteOperation operation)
    {
        /// <summary>Returns the bounded lowercase value shared by journal logs, activities, and metrics.</summary>
        /// <returns>A stable lowercase operation value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="operation"/> is undefined.</exception>
        internal string ToStableValue()
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(operation);
            return operation switch
            {
                DurableJournalWriteOperation.RecordStart => "record_start",
                DurableJournalWriteOperation.RecordCheckpoint => "record_checkpoint",
                DurableJournalWriteOperation.RecordTerminal => "record_terminal",
                _ => throw new UnreachableException(),
            };
        }
    }
}
