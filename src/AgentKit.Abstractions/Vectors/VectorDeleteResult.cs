// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of a vector deletion: the count removed and the index watermark, or a typed refusal.</summary>
public sealed record VectorDeleteResult
{
    private VectorDeleteResult(int deleted, long watermark, MemoryStoreFailure? failure)
    {
        Deleted = deleted;
        Watermark = watermark;
        Failure = failure;
    }

    /// <summary>Gets the number of vectors actually removed; absent chunks are not counted.</summary>
    public int Deleted { get; }

    /// <summary>Gets the index watermark after the request.</summary>
    public long Watermark { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> when the deletion ran.</summary>
    public MemoryStoreFailure? Failure { get; }

    /// <summary>Gets a value indicating whether the deletion ran.</summary>
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsDeleted => Failure is null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="deleted">The number of vectors removed.</param>
    /// <param name="watermark">The index watermark after the request.</param>
    /// <returns>A deleted result.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deleted"/> or <paramref name="watermark"/> is negative.</exception>
    public static VectorDeleteResult Succeeded(int deleted, long watermark)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deleted);
        ArgumentOutOfRangeException.ThrowIfNegative(watermark);
        return new(deleted, watermark, null);
    }

    /// <summary>Creates a refused result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static VectorDeleteResult Rejected(MemoryStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(0, 0, failure);
    }
}
