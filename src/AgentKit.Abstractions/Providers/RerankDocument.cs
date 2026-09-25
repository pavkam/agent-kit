// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One document supplied to a rerank request.</summary>
public sealed record RerankDocument
{
    /// <summary>Initializes a rerank document.</summary>
    /// <param name="id">The caller correlation identity.</param>
    /// <param name="inputIndex">The zero-based index in the request array.</param>
    /// <param name="text">The document text.</param>
    /// <param name="metadata">Provider-neutral metadata.</param>
    /// <exception cref="ArgumentException"><paramref name="text"/> is invalid.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="metadata"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inputIndex"/> is negative.</exception>
    public RerankDocument(DocumentId id, int inputIndex, string text, ExtensionData metadata)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(metadata);
        Id = id;
        InputIndex = inputIndex;
        Text = text;
        Metadata = metadata;
    }

    /// <summary>Gets the caller correlation identity.</summary>
    public DocumentId Id { get; init; }

    /// <summary>Gets the zero-based index in the request array.</summary>
    public int InputIndex { get; init; }

    /// <summary>Gets the document text.</summary>
    public string Text { get; init; }

    /// <summary>Gets provider-neutral metadata.</summary>
    public ExtensionData Metadata { get; init; }
}
