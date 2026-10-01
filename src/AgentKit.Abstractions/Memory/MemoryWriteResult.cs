// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of a memory write: the durable record, or a typed refusal.</summary>
public sealed record MemoryWriteResult
{
    private MemoryWriteResult(DurableMemoryRecord? record, bool replayed, MemoryStoreFailure? failure)
    {
        Record = record;
        Replayed = replayed;
        Failure = failure;
    }

    /// <summary>Gets the durable record, or <see langword="null"/> when the write was refused.</summary>
    public DurableMemoryRecord? Record { get; }

    /// <summary>Gets a value indicating whether an earlier equivalent request had already created the record.</summary>
    public bool Replayed { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> when the write succeeded.</summary>
    public MemoryStoreFailure? Failure { get; }

    /// <summary>Gets a value indicating whether the record is durable.</summary>
    [MemberNotNullWhen(true, nameof(Record))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsWritten => Record is not null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="record">The durable record.</param>
    /// <param name="replayed">Whether the record came from an idempotent replay.</param>
    /// <returns>A written result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    public static MemoryWriteResult Written(DurableMemoryRecord record, bool replayed)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new(record, replayed, null);
    }

    /// <summary>Creates a refused result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static MemoryWriteResult Rejected(MemoryStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(null, false, failure);
    }
}
