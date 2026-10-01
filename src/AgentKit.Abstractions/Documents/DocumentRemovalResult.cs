// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of a document removal: the receipt with per-index cleanup accounting, or a typed refusal.</summary>
public sealed record DocumentRemovalResult
{
    private DocumentRemovalResult(DocumentDeletionReceipt? receipt, int vectorsRemoved, bool replayed, MemoryStoreFailure? failure)
    {
        Receipt = receipt;
        VectorsRemoved = vectorsRemoved;
        Replayed = replayed;
        Failure = failure;
    }

    /// <summary>Gets the final deletion receipt, or <see langword="null"/> when refused.</summary>
    /// <remarks>The receipt's pending stores name every vector index whose cleanup did not complete, and the body is purged only when none are pending.</remarks>
    public DocumentDeletionReceipt? Receipt { get; }

    /// <summary>Gets the number of vectors removed across every index.</summary>
    public int VectorsRemoved { get; }

    /// <summary>Gets a value indicating whether the receipt came from an idempotent replay.</summary>
    public bool Replayed { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> when the deletion was recorded.</summary>
    public MemoryStoreFailure? Failure { get; }

    /// <summary>Gets a value indicating whether a receipt was produced.</summary>
    [MemberNotNullWhen(true, nameof(Receipt))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsDeleted => Receipt is not null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="receipt">The final deletion receipt.</param>
    /// <param name="vectorsRemoved">The number of vectors removed across every index.</param>
    /// <param name="replayed">Whether the receipt came from an idempotent replay.</param>
    /// <returns>A deleted result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="receipt"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="vectorsRemoved"/> is negative.</exception>
    public static DocumentRemovalResult Deleted(DocumentDeletionReceipt receipt, int vectorsRemoved, bool replayed)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentOutOfRangeException.ThrowIfNegative(vectorsRemoved);
        return new(receipt, vectorsRemoved, replayed, null);
    }

    /// <summary>Creates a refused result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static DocumentRemovalResult Rejected(MemoryStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(null, 0, false, failure);
    }
}
