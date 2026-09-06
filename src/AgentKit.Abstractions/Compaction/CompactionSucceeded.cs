// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A compaction attempt that was validated and durably activated.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record CompactionSucceeded: CompactionResult
{
    /// <summary>Initializes a new instance of the <see cref="CompactionSucceeded"/> record.</summary>
    /// <param name="context">The operation context this outcome resulted from.</param>
    /// <param name="record">The activated compaction record.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="record"/> is null.
    /// </exception>
    public CompactionSucceeded(CompactionOperationContext context, CompactionRecord record)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(record);
        Record = record;
    }

    /// <summary>Gets the activated compaction record.</summary>
    public CompactionRecord Record { get; init; }
}
