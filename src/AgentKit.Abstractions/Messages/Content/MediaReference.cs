// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A reference to one media item carried by a <see cref="MediaReferencePart"/>.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields (including a byte-for-byte comparison of
/// <see cref="InlineBytes"/> when populated). It carries no mutable state
/// and is safe to share across threads without synchronization. Exactly one
/// of <see cref="Uri"/> or <see cref="InlineBytes"/> is meaningful,
/// depending on <see cref="SourceKind"/>; consumers must check
/// <see cref="SourceKind"/> before reading either rather than guessing from
/// which one happens to be populated.
/// </remarks>
public sealed record MediaReference
{
    /// <summary>Initializes a new instance of the <see cref="MediaReference"/> record.</summary>
    /// <param name="id">The stable identity of this media reference.</param>
    /// <param name="sourceKind">Where the underlying bytes come from.</param>
    /// <param name="mediaType">The IANA media (MIME) type of the content.</param>
    /// <param name="uri">
    /// The remote location of the content when <paramref name="sourceKind"/>
    /// is <see cref="MediaSourceKind.Uri"/>; otherwise <see langword="null"/>.
    /// </param>
    /// <param name="inlineBytes">
    /// The embedded content when <paramref name="sourceKind"/> is
    /// <see cref="MediaSourceKind.InlineBytes"/>; otherwise empty. A
    /// <see cref="MediaSourceKind.Uri"/>- or
    /// <see cref="MediaSourceKind.FileReference"/>-sourced medium never
    /// inlines its bytes into a durable message merely because it was
    /// observed once.
    /// </param>
    /// <param name="sizeInBytes">The content size in bytes, when known.</param>
    /// <param name="hash">A content-addressable hash of the referenced bytes, when known.</param>
    /// <param name="extensions">Provider-specific or forward-compatible data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="mediaType"/> or <paramref name="extensions"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="inlineBytes"/> is a default, uninitialized array.
    /// </exception>
    public MediaReference(
        MediaId id,
        MediaSourceKind sourceKind,
        string mediaType,
        Uri? uri,
        ImmutableArray<byte> inlineBytes,
        long? sizeInBytes,
        ContentHash? hash,
        ExtensionData extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentException.ThrowIfDefault(inlineBytes);
        ArgumentNullException.ThrowIfNull(extensions);

        Id = id;
        SourceKind = sourceKind;
        MediaType = mediaType;
        Uri = uri;
        InlineBytes = inlineBytes;
        SizeInBytes = sizeInBytes;
        Hash = hash;
        Extensions = extensions;
    }

    /// <summary>Gets the stable identity of this media reference.</summary>
    public MediaId Id { get; init; }

    /// <summary>Gets where the underlying bytes come from.</summary>
    public MediaSourceKind SourceKind { get; init; }

    /// <summary>Gets the IANA media (MIME) type of the content.</summary>
    public string MediaType { get; init; }

    /// <summary>
    /// Gets the remote location of the content when <see cref="SourceKind"/>
    /// is <see cref="MediaSourceKind.Uri"/>; otherwise <see langword="null"/>.
    /// </summary>
    public Uri? Uri { get; init; }

    /// <summary>
    /// Gets the embedded content when <see cref="SourceKind"/> is
    /// <see cref="MediaSourceKind.InlineBytes"/>; otherwise empty.
    /// </summary>
    public ImmutableArray<byte> InlineBytes { get; init; }

    /// <summary>Gets the content size in bytes, when known.</summary>
    public long? SizeInBytes { get; init; }

    /// <summary>Gets a content-addressable hash of the referenced bytes, when known.</summary>
    public ContentHash? Hash { get; init; }

    /// <summary>Gets provider-specific or forward-compatible data.</summary>
    public ExtensionData Extensions { get; init; }

    /// <inheritdoc/>
    public bool Equals(MediaReference? other) =>
        other is not null
        && Id.Equals(other.Id)
        && SourceKind == other.SourceKind
        && MediaType == other.MediaType
        && Uri == other.Uri
        && InlineBytes.SequenceEqual(other.InlineBytes)
        && SizeInBytes == other.SizeInBytes
        && Nullable.Equals(Hash, other.Hash)
        && Extensions.Equals(other.Extensions);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(SourceKind);
        hash.Add(MediaType);
        hash.Add(Uri);
        foreach (var b in InlineBytes)
        {
            hash.Add(b);
        }

        hash.Add(SizeInBytes);
        hash.Add(Hash);
        hash.Add(Extensions);
        return hash.ToHashCode();
    }
}
