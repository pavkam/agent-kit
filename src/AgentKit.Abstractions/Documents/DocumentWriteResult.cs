// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of publishing a document version: the stored version's state, or a typed refusal.</summary>
public sealed record DocumentWriteResult
{
    private DocumentWriteResult(DocumentRecord? record, DocumentVersionState state, DocumentVersion? previousActive, bool replayed, MemoryStoreFailure? failure)
    {
        Record = record;
        State = state;
        PreviousActiveVersion = previousActive;
        Replayed = replayed;
        Failure = failure;
    }

    /// <summary>Gets the stored version's record, or <see langword="null"/> when refused.</summary>
    public DocumentRecord? Record { get; }

    /// <summary>Gets the stored version's publication state.</summary>
    /// <value>Meaningful only when the write succeeded.</value>
    public DocumentVersionState State { get; }

    /// <summary>Gets the version that was active before this write switched the pointer, or <see langword="null"/>.</summary>
    public DocumentVersion? PreviousActiveVersion { get; }

    /// <summary>Gets a value indicating whether an earlier equivalent request had already published the version.</summary>
    public bool Replayed { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> when the version was stored.</summary>
    public MemoryStoreFailure? Failure { get; }

    /// <summary>Gets a value indicating whether the version was stored.</summary>
    [MemberNotNullWhen(true, nameof(Record))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsWritten => Record is not null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="record">The stored version's record.</param>
    /// <param name="state">The version's publication state.</param>
    /// <param name="previousActive">The version that was active before the pointer switched, or <see langword="null"/>.</param>
    /// <param name="replayed">Whether the result came from an idempotent replay.</param>
    /// <returns>A written result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is undefined.</exception>
    public static DocumentWriteResult Written(DocumentRecord record, DocumentVersionState state, DocumentVersion? previousActive, bool replayed)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentOutOfRangeException.ThrowIfUndefined(state);
        return new(record, state, previousActive, replayed, null);
    }

    /// <summary>Creates a refused result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static DocumentWriteResult Rejected(MemoryStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(null, DocumentVersionState.Staged, null, false, failure);
    }
}
