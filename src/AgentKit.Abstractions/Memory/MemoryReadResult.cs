// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of a point read: the record, a content-free tombstone, or a typed refusal.</summary>
/// <remarks>A logically deleted memory never yields its body. The store returns <see cref="Tombstone"/> instead, so deletion is auditable without leaking deleted content.</remarks>
public sealed record MemoryReadResult
{
    private MemoryReadResult(DurableMemoryRecord? record, MemoryTombstone? tombstone, MemoryStoreFailure? failure)
    {
        Record = record;
        Tombstone = tombstone;
        Failure = failure;
    }

    /// <summary>Gets the record, or <see langword="null"/> when the read found none.</summary>
    public DurableMemoryRecord? Record { get; }

    /// <summary>Gets the tombstone of a deleted memory, or <see langword="null"/>.</summary>
    public MemoryTombstone? Tombstone { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> when the read produced a record or tombstone.</summary>
    public MemoryStoreFailure? Failure { get; }

    /// <summary>Gets a value indicating whether a readable record was found.</summary>
    [MemberNotNullWhen(true, nameof(Record))]
    public bool IsFound => Record is not null;

    /// <summary>Creates a result carrying a readable record.</summary>
    /// <param name="record">The record.</param>
    /// <returns>A found result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    public static MemoryReadResult Found(DurableMemoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new(record, null, null);
    }

    /// <summary>Creates a result carrying the tombstone of a deleted memory.</summary>
    /// <param name="tombstone">The tombstone.</param>
    /// <returns>A tombstoned result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tombstone"/> is null.</exception>
    public static MemoryReadResult Tombstoned(MemoryTombstone tombstone)
    {
        ArgumentNullException.ThrowIfNull(tombstone);
        return new(null, tombstone, null);
    }

    /// <summary>Creates a refused result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static MemoryReadResult Rejected(MemoryStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(null, null, failure);
    }
}
