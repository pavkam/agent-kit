// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of an active-version switch: the newly active record, or a typed refusal.</summary>
public sealed record DocumentActivateResult
{
    private DocumentActivateResult(DocumentRecord? record, DocumentVersion? previousActive, bool replayed, MemoryStoreFailure? failure)
    {
        Record = record;
        PreviousActiveVersion = previousActive;
        Replayed = replayed;
        Failure = failure;
    }

    /// <summary>Gets the newly active version's record, or <see langword="null"/> when refused.</summary>
    public DocumentRecord? Record { get; }

    /// <summary>Gets the version that was active before the switch, or <see langword="null"/>.</summary>
    public DocumentVersion? PreviousActiveVersion { get; }

    /// <summary>Gets a value indicating whether the version was already active under an equivalent request.</summary>
    public bool Replayed { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> when the switch applied.</summary>
    public MemoryStoreFailure? Failure { get; }

    /// <summary>Gets a value indicating whether the version is active.</summary>
    [MemberNotNullWhen(true, nameof(Record))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsActivated => Record is not null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="record">The newly active version's record.</param>
    /// <param name="previousActive">The previously active version, or <see langword="null"/>.</param>
    /// <param name="replayed">Whether the result came from an idempotent replay.</param>
    /// <returns>An activated result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    public static DocumentActivateResult Activated(DocumentRecord record, DocumentVersion? previousActive, bool replayed)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new(record, previousActive, replayed, null);
    }

    /// <summary>Creates a refused result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static DocumentActivateResult Rejected(MemoryStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(null, null, false, failure);
    }
}
