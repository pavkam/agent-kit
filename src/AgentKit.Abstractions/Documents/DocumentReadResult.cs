// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of a document read: the version with its chunks, the content-free receipt of a deleted document, or a typed refusal.</summary>
public sealed record DocumentReadResult
{
    private DocumentReadResult(
        DocumentRecord? record,
        DocumentVersionState state,
        ImmutableArray<DocumentChunk> chunks,
        DocumentVersion? activeVersion,
        DocumentDeletionReceipt? tombstone,
        MemoryStoreFailure? failure)
    {
        Record = record;
        State = state;
        Chunks = chunks;
        ActiveVersion = activeVersion;
        Tombstone = tombstone;
        Failure = failure;
    }

    /// <summary>Gets the version's record, or <see langword="null"/> when none was found.</summary>
    public DocumentRecord? Record { get; }

    /// <summary>Gets the version's publication state.</summary>
    /// <value>Meaningful only when <see cref="Record"/> is present.</value>
    public DocumentVersionState State { get; }

    /// <summary>Gets the version's chunk set when requested, in ordinal order; empty otherwise.</summary>
    public ImmutableArray<DocumentChunk> Chunks { get; }

    /// <summary>Gets the document's currently active version, or <see langword="null"/> when none is active.</summary>
    public DocumentVersion? ActiveVersion { get; }

    /// <summary>Gets the content-free deletion evidence of a deleted document, or <see langword="null"/>.</summary>
    public DocumentDeletionReceipt? Tombstone { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> when a version or tombstone was produced.</summary>
    public MemoryStoreFailure? Failure { get; }

    /// <summary>Gets a value indicating whether a readable version was found.</summary>
    [MemberNotNullWhen(true, nameof(Record))]
    public bool IsFound => Record is not null;

    /// <summary>Creates a result carrying a readable version.</summary>
    /// <param name="record">The version's record.</param>
    /// <param name="state">The version's publication state.</param>
    /// <param name="chunks">The chunk set, or default for none.</param>
    /// <param name="activeVersion">The currently active version, or <see langword="null"/>.</param>
    /// <returns>A found result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is undefined.</exception>
    public static DocumentReadResult Found(DocumentRecord record, DocumentVersionState state, ImmutableArray<DocumentChunk> chunks, DocumentVersion? activeVersion)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentOutOfRangeException.ThrowIfUndefined(state);
        return new(record, state, chunks.IsDefault ? [] : chunks, activeVersion, null, null);
    }

    /// <summary>Creates a result carrying the deletion evidence of a deleted document.</summary>
    /// <param name="tombstone">The content-free receipt.</param>
    /// <returns>A tombstoned result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tombstone"/> is null.</exception>
    public static DocumentReadResult Tombstoned(DocumentDeletionReceipt tombstone)
    {
        ArgumentNullException.ThrowIfNull(tombstone);
        return new(null, DocumentVersionState.Staged, [], null, tombstone, null);
    }

    /// <summary>Creates a refused result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static DocumentReadResult Rejected(MemoryStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(null, DocumentVersionState.Staged, [], null, null, failure);
    }
}
