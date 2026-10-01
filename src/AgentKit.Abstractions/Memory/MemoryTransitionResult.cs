// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of a lifecycle transition: the updated record and any replacement, or a typed refusal.</summary>
public sealed record MemoryTransitionResult
{
    private MemoryTransitionResult(DurableMemoryRecord? record, DurableMemoryRecord? replacement, bool replayed, MemoryStoreFailure? failure)
    {
        Record = record;
        Replacement = replacement;
        Replayed = replayed;
        Failure = failure;
    }

    /// <summary>Gets the record after the transition, or <see langword="null"/> when refused.</summary>
    public DurableMemoryRecord? Record { get; }

    /// <summary>Gets the replacement record a correction created, or <see langword="null"/>.</summary>
    public DurableMemoryRecord? Replacement { get; }

    /// <summary>Gets a value indicating whether an earlier equivalent request had already applied the transition.</summary>
    public bool Replayed { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> when the transition applied.</summary>
    public MemoryStoreFailure? Failure { get; }

    /// <summary>Gets a value indicating whether the transition applied.</summary>
    [MemberNotNullWhen(true, nameof(Record))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsTransitioned => Record is not null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="record">The record after the transition.</param>
    /// <param name="replacement">The replacement a correction created, or <see langword="null"/>.</param>
    /// <param name="replayed">Whether the result came from an idempotent replay.</param>
    /// <returns>A transitioned result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    public static MemoryTransitionResult Transitioned(DurableMemoryRecord record, DurableMemoryRecord? replacement, bool replayed)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new(record, replacement, replayed, null);
    }

    /// <summary>Creates a refused result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static MemoryTransitionResult Rejected(MemoryStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(null, null, false, failure);
    }
}
