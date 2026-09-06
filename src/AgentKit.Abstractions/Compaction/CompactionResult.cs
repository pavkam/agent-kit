// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The closed outcome of one complete compaction attempt: exactly one of
/// <see cref="CompactionSucceeded"/>, <see cref="CompactionConflict"/>,
/// <see cref="CompactionRejected"/>, <see cref="CompactionNotReducing"/>,
/// <see cref="CompactionCancelled"/>, or <see cref="CompactionFailed"/>.
/// </summary>
/// <remarks>
/// This hierarchy is closed to first-party outcomes recognized by
/// <see cref="ICompactor"/> implementations and their callers; external
/// assemblies cannot derive additional cases. Each instance is immutable
/// and safe to share across threads without synchronization. Every case
/// carries the originating <see cref="CompactionOperationContext"/> so a
/// caller can correlate the outcome back to its request without a separate
/// lookup.
/// </remarks>
public abstract record CompactionResult
{
    private protected CompactionResult(CompactionOperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
    }

    /// <summary>Gets the operation context this outcome resulted from.</summary>
    public CompactionOperationContext Context { get; init; }
}
