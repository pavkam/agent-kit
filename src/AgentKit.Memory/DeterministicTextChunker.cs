// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>Splits document text into deterministic, paragraph-aware chunks whose identities derive from content.</summary>
/// <remarks>
/// <para>
/// The chunker splits on blank-line paragraph boundaries, packs whole paragraphs into chunks up to <see cref="MaximumChunkCharacters"/>
/// characters, and hard-splits a single paragraph longer than that bound at the bound. Chunk identity is a SHA-256 derived
/// <see cref="ChunkId"/> over the document id, document version, chunker version, ordinal, and chunk text, so the same source
/// content, version, and chunker always yield the same chunk identities, while a different chunker version or changed content can
/// never reuse a stale identity. The content hash is the SHA-256 of the chunk's UTF-8 text.
/// </para>
/// <para>The chunker is stateless, allocation-bounded by the input, and thread-safe. Whitespace-only text yields no chunks.</para>
/// </remarks>
internal sealed class DeterministicTextChunker: IDocumentChunker
{
    /// <summary>The maximum number of characters one chunk holds.</summary>
    internal const int MaximumChunkCharacters = 1_200;

    private static readonly ChunkerVersion _version = new("agentkit.paragraph-v1");

    /// <inheritdoc/>
    public ChunkerVersion Version => _version;

    /// <inheritdoc/>
    public ImmutableArray<DocumentChunk> Chunk(DocumentId documentId, DocumentVersion version, string text)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(documentId, default, nameof(documentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentNullException.ThrowIfNull(text);
        var chunks = ImmutableArray.CreateBuilder<DocumentChunk>();
        var current = new StringBuilder();
        foreach (var paragraph in Paragraphs(text))
        {
            foreach (var piece in Split(paragraph))
            {
                if (current.Length > 0 && current.Length + piece.Length + 2 > MaximumChunkCharacters)
                {
                    Flush(current, chunks, documentId, version);
                }

                _ = current.Length > 0 ? current.Append("\n\n") : current;
                _ = current.Append(piece);
            }
        }

        Flush(current, chunks, documentId, version);
        return chunks.ToImmutable();
    }

    private static string[] Paragraphs(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IEnumerable<string> Split(string paragraph)
    {
        for (var offset = 0; offset < paragraph.Length; offset += MaximumChunkCharacters)
        {
            yield return paragraph.Substring(offset, Math.Min(MaximumChunkCharacters, paragraph.Length - offset));
        }
    }

    private static void Flush(StringBuilder current, ImmutableArray<DocumentChunk>.Builder chunks, DocumentId documentId, DocumentVersion version)
    {
        if (current.Length == 0)
        {
            return;
        }

        var text = current.ToString();
        _ = current.Clear();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var ordinal = chunks.Count;
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var identity = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            '|', documentId.ToString(), version.Value, _version.Value, ordinal.ToString(CultureInfo.InvariantCulture), hash)));
        chunks.Add(new DocumentChunk(new ChunkId(new Guid(identity.AsSpan(0, 16))), documentId, version, _version, ordinal, text, new ContentHash($"sha256:{hash}")));
    }
}
