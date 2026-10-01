// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the persisted form of one <see cref="DocumentVersionEntry"/>.</summary>
/// <param name="Record">The version's record.</param>
/// <param name="Chunks">The version's chunk set.</param>
/// <param name="State">The publication state.</param>
internal sealed record DocumentVersionDocument(DocumentRecordDocument Record, ImmutableArray<DocumentChunkDocument> Chunks, DocumentVersionState State)
{
    /// <summary>Converts a version to its persisted form.</summary>
    /// <param name="value">The non-null version.</param>
    /// <returns>The document.</returns>
    internal static DocumentVersionDocument FromDomain(DocumentVersionEntry value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(DocumentRecordDocument.FromDomain(value.Record), [.. value.Chunks.Select(DocumentChunkDocument.FromDomain)], value.State);
    }

    /// <summary>Restores the version, re-running its validation.</summary>
    /// <returns>The version.</returns>
    internal DocumentVersionEntry ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Record);
        var chunks = Chunks.IsDefault ? [] : Chunks;
        ArgumentException.ThrowIfContainsNull(chunks, nameof(Chunks));
        return new(Record.ToDomain(), [.. chunks.Select(static chunk => chunk.ToDomain())], State);
    }
}
