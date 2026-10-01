// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of a document deletion: a receipt separating logical and physical deletion, or a typed refusal.</summary>
public sealed record DocumentDeleteResult
{
    private DocumentDeleteResult(DocumentDeletionReceipt? receipt, bool replayed, MemoryStoreFailure? failure)
    {
        Receipt = receipt;
        Replayed = replayed;
        Failure = failure;
    }

    /// <summary>Gets the deletion receipt, or <see langword="null"/> when refused.</summary>
    public DocumentDeletionReceipt? Receipt { get; }

    /// <summary>Gets a value indicating whether the receipt came from an idempotent replay.</summary>
    public bool Replayed { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> when the deletion was recorded.</summary>
    public MemoryStoreFailure? Failure { get; }

    /// <summary>Gets a value indicating whether a receipt was produced.</summary>
    [MemberNotNullWhen(true, nameof(Receipt))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsDeleted => Receipt is not null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="receipt">The deletion receipt.</param>
    /// <param name="replayed">Whether the receipt came from an idempotent replay.</param>
    /// <returns>A deleted result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="receipt"/> is null.</exception>
    public static DocumentDeleteResult Deleted(DocumentDeletionReceipt receipt, bool replayed)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return new(receipt, replayed, null);
    }

    /// <summary>Creates a refused result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static DocumentDeleteResult Rejected(MemoryStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(null, false, failure);
    }
}
