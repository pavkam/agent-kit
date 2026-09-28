// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures immutable source evidence for a memory record, document, or retrieval candidate.</summary>
/// <remarks>
/// Provenance survives rewriting, reranking, deduplication, and context trimming. It never grants authority.
/// </remarks>
public sealed record Provenance
{
    /// <summary>Initializes provenance with a non-blank source kind and extension payload.</summary>
    /// <param name="sourceKind">The stable kind label for the originating surface.</param>
    /// <param name="sourceRunId">The run that produced the content, when applicable.</param>
    /// <param name="sourceSessionId">The session that produced the content, when applicable.</param>
    /// <param name="sourceMessageId">The message that produced the content, when applicable.</param>
    /// <param name="extensions">Additional provider-neutral provenance fields.</param>
    /// <exception cref="ArgumentException"><paramref name="sourceKind"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public Provenance(
        string sourceKind,
        RunId? sourceRunId = null,
        SessionId? sourceSessionId = null,
        MessageId? sourceMessageId = null,
        ExtensionData? extensions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKind);
        SourceKind = sourceKind;
        SourceRunId = sourceRunId;
        SourceSessionId = sourceSessionId;
        SourceMessageId = sourceMessageId;
        Extensions = extensions ?? ExtensionData.Empty;
    }

    /// <summary>Gets the stable kind label for the originating surface.</summary>
    public string SourceKind { get; init; }

    /// <summary>Gets the run that produced the content, when applicable.</summary>
    public RunId? SourceRunId { get; init; }

    /// <summary>Gets the session that produced the content, when applicable.</summary>
    public SessionId? SourceSessionId { get; init; }

    /// <summary>Gets the message that produced the content, when applicable.</summary>
    public MessageId? SourceMessageId { get; init; }

    /// <summary>Gets additional provider-neutral provenance fields.</summary>
    public ExtensionData Extensions { get; init; }
}
