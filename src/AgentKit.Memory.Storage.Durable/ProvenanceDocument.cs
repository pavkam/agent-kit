// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the persisted form of a <see cref="Provenance"/>.</summary>
/// <param name="SourceKind">The source kind label.</param>
/// <param name="SourceRunId">The producing run, or <see langword="null"/>.</param>
/// <param name="SourceSessionId">The producing session, or <see langword="null"/>.</param>
/// <param name="SourceMessageId">The producing message, or <see langword="null"/>.</param>
/// <param name="Extensions">The provenance extension data.</param>
internal sealed record ProvenanceDocument(
    string SourceKind,
    Guid? SourceRunId,
    Guid? SourceSessionId,
    Guid? SourceMessageId,
    ImmutableArray<StoreExtensionDocument> Extensions)
{
    /// <summary>Converts provenance to its persisted form.</summary>
    /// <param name="value">The non-null provenance.</param>
    /// <returns>The document.</returns>
    internal static ProvenanceDocument FromDomain(Provenance value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.SourceKind,
            value.SourceRunId?.Value,
            value.SourceSessionId?.Value,
            value.SourceMessageId?.Value,
            StoreExtensionDocument.FromDomain(value.Extensions));
    }

    /// <summary>Restores the provenance, re-running its validation.</summary>
    /// <returns>The provenance.</returns>
    internal Provenance ToDomain() => new(
        SourceKind,
        SourceRunId is { } run ? new RunId(run) : null,
        SourceSessionId is { } session ? new SessionId(session) : null,
        SourceMessageId is { } message ? new MessageId(message) : null,
        StoreExtensionDocument.ToDomain(Extensions));
}
