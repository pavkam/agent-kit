// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of a list read: one page of records with a cursor and deletion watermark, or a typed refusal.</summary>
public sealed record MemoryListResult
{
    private MemoryListResult(ImmutableArray<DurableMemoryRecord> items, long? nextCursor, long deletionGeneration, MemoryStoreFailure? failure)
    {
        Items = items;
        NextCursor = nextCursor;
        DeletionGeneration = deletionGeneration;
        Failure = failure;
    }

    /// <summary>Gets the records in ascending store sequence order.</summary>
    /// <value>Empty when the read was refused.</value>
    public ImmutableArray<DurableMemoryRecord> Items { get; }

    /// <summary>Gets the sequence to pass as the next exclusive cursor, or <see langword="null"/> when the page was the last.</summary>
    public long? NextCursor { get; }

    /// <summary>Gets the store's deletion generation when the page was read.</summary>
    /// <value>Zero before any deletion. Retrieval binds this watermark to its exposure decision.</value>
    public long DeletionGeneration { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> when a page was produced.</summary>
    public MemoryStoreFailure? Failure { get; }

    /// <summary>Gets a value indicating whether a page was produced.</summary>
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsPage => Failure is null;

    /// <summary>Creates a page result.</summary>
    /// <param name="items">The records in sequence order.</param>
    /// <param name="nextCursor">The next exclusive cursor, or <see langword="null"/> for the last page.</param>
    /// <param name="deletionGeneration">The deletion generation at read time.</param>
    /// <returns>A page result.</returns>
    /// <exception cref="ArgumentException"><paramref name="items"/> is default or contains null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nextCursor"/> or <paramref name="deletionGeneration"/> is negative.</exception>
    public static MemoryListResult Page(ImmutableArray<DurableMemoryRecord> items, long? nextCursor, long deletionGeneration)
    {
        ArgumentException.ThrowIfDefault(items);
        ArgumentException.ThrowIfContainsNull(items);
        if (nextCursor is { } cursor)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(cursor, nameof(nextCursor));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(deletionGeneration);
        return new(items, nextCursor, deletionGeneration, null);
    }

    /// <summary>Creates a refused result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static MemoryListResult Rejected(MemoryStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new([], null, 0, failure);
    }
}
