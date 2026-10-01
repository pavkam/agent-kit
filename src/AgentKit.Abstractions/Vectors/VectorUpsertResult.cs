// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of a vector upsert: the count stored and the index watermark, or a typed refusal.</summary>
public sealed record VectorUpsertResult
{
    private VectorUpsertResult(int upserted, long watermark, bool replayed, MemoryStoreFailure? failure)
    {
        Upserted = upserted;
        Watermark = watermark;
        Replayed = replayed;
        Failure = failure;
    }

    /// <summary>Gets the number of vectors stored by the request.</summary>
    public int Upserted { get; }

    /// <summary>Gets the index watermark after the request.</summary>
    public long Watermark { get; }

    /// <summary>Gets a value indicating whether an earlier equivalent request had already stored the batch.</summary>
    public bool Replayed { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> when the batch was stored.</summary>
    public MemoryStoreFailure? Failure { get; }

    /// <summary>Gets a value indicating whether the batch was stored.</summary>
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsUpserted => Failure is null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="upserted">The number of vectors stored.</param>
    /// <param name="watermark">The index watermark after the request.</param>
    /// <param name="replayed">Whether the result came from an idempotent replay.</param>
    /// <returns>An upserted result.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="upserted"/> or <paramref name="watermark"/> is negative.</exception>
    public static VectorUpsertResult Succeeded(int upserted, long watermark, bool replayed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(upserted);
        ArgumentOutOfRangeException.ThrowIfNegative(watermark);
        return new(upserted, watermark, replayed, null);
    }

    /// <summary>Creates a refused result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static VectorUpsertResult Rejected(MemoryStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(0, 0, false, failure);
    }
}
