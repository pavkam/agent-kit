// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;

/// <summary>
/// A reference to one media item carried by a <see cref="MediaReferencePart"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields (including a byte-for-byte comparison of
/// <see cref="InlineBytes"/> when populated). It carries no mutable state
/// and is safe to share across threads without synchronization.
/// </para>
/// <para>
/// <see cref="SourceKind"/> decides which locator fields are populated, and
/// the constructor enforces that combination: an
/// <see cref="MediaSourceKind.InlineBytes"/> medium carries at least one
/// byte and no <see cref="Uri"/>; a <see cref="MediaSourceKind.Uri"/> medium
/// carries a <see cref="Uri"/> and no bytes; a
/// <see cref="MediaSourceKind.FileReference"/> medium carries no bytes and
/// may retain a locator <see cref="Uri"/> as evidence that is never resolved
/// merely because it is present. Because those three members must agree with
/// one another, they are read-only rather than <see langword="init"/>
/// members: a <c>with</c> expression cannot change one without the others, so
/// callers construct a new reference instead. Consumers still check
/// <see cref="SourceKind"/> before reading a locator rather than guessing from
/// which one happens to be populated.
/// </para>
/// </remarks>
public sealed record MediaReference
{
    /// <summary>Initializes a new instance of the <see cref="MediaReference"/> record.</summary>
    /// <param name="id">The stable identity of this media reference.</param>
    /// <param name="sourceKind">Where the underlying bytes come from; must be a defined <see cref="MediaSourceKind"/>.</param>
    /// <param name="mediaType">The IANA media (MIME) type of the content.</param>
    /// <param name="uri">
    /// The remote location of the content, required when
    /// <paramref name="sourceKind"/> is <see cref="MediaSourceKind.Uri"/>,
    /// optional locator evidence when it is
    /// <see cref="MediaSourceKind.FileReference"/>, and
    /// <see langword="null"/> when it is <see cref="MediaSourceKind.InlineBytes"/>.
    /// </param>
    /// <param name="inlineBytes">
    /// The embedded content, at least one byte, when
    /// <paramref name="sourceKind"/> is <see cref="MediaSourceKind.InlineBytes"/>;
    /// otherwise empty. A <see cref="MediaSourceKind.Uri"/>- or
    /// <see cref="MediaSourceKind.FileReference"/>-sourced medium never
    /// inlines its bytes into a durable message merely because it was
    /// observed once.
    /// </param>
    /// <param name="sizeInBytes">The nonnegative content size in bytes, when known.</param>
    /// <param name="hash">A content-addressable hash of the referenced bytes, when known.</param>
    /// <param name="extensions">Provider-specific or forward-compatible data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="extensions"/> is null, or <paramref name="uri"/> is null while
    /// <paramref name="sourceKind"/> is <see cref="MediaSourceKind.Uri"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sourceKind"/> is not a defined <see cref="MediaSourceKind"/>, or
    /// <paramref name="sizeInBytes"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="mediaType"/> is null, empty, or whitespace; <paramref name="inlineBytes"/> is a
    /// default, uninitialized array; <paramref name="inlineBytes"/> is empty for an inline medium or
    /// nonempty for a referenced medium; <paramref name="uri"/> is supplied for an inline medium; or
    /// <paramref name="sizeInBytes"/> is supplied for an inline medium and disagrees with
    /// <paramref name="inlineBytes"/>'s length.
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
        ArgumentOutOfRangeException.ThrowIfUndefined(sourceKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentException.ThrowIfDefault(inlineBytes);
        switch (sourceKind)
        {
            case MediaSourceKind.InlineBytes:
                ArgumentException.ThrowIfNotEqual(uri, null, nameof(uri));
                ArgumentException.ThrowIfDefaultOrEmpty(inlineBytes);
                break;
            case MediaSourceKind.Uri:
                ArgumentNullException.ThrowIfNull(uri);
                ArgumentException.ThrowIfNotEqual(inlineBytes.IsEmpty, true, nameof(inlineBytes));
                break;
            case MediaSourceKind.FileReference:
            default:
                Debug.Assert(sourceKind == MediaSourceKind.FileReference, "ThrowIfUndefined rejected every other value above.");
                ArgumentException.ThrowIfNotEqual(inlineBytes.IsEmpty, true, nameof(inlineBytes));
                break;
        }

        if (sizeInBytes is { } size)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(size, nameof(sizeInBytes));
            if (sourceKind == MediaSourceKind.InlineBytes && size != inlineBytes.Length)
            {
                throw new ArgumentException(
                    "Value must equal the inline byte count when both are supplied.",
                    nameof(sizeInBytes));
            }
        }

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
    /// <value>A defined <see cref="MediaSourceKind"/> that governs which locator members are populated.</value>
    public MediaSourceKind SourceKind { get; }

    /// <summary>Gets the IANA media (MIME) type of the content.</summary>
    /// <exception cref="ArgumentException">
    /// The value assigned during initialization or non-destructive mutation is null, empty, or
    /// consists only of whitespace.
    /// </exception>
    public string MediaType
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    }

    /// <summary>
    /// Gets the remote location of the content when <see cref="SourceKind"/>
    /// is <see cref="MediaSourceKind.Uri"/>, or an optional unresolved locator
    /// when it is <see cref="MediaSourceKind.FileReference"/>; otherwise
    /// <see langword="null"/>.
    /// </summary>
    /// <value>Never null for a <see cref="MediaSourceKind.Uri"/> medium; always null for an inline medium.</value>
    public Uri? Uri { get; }

    /// <summary>
    /// Gets the embedded content when <see cref="SourceKind"/> is
    /// <see cref="MediaSourceKind.InlineBytes"/>; otherwise empty.
    /// </summary>
    /// <value>An initialized array that is nonempty exactly when the medium is inline.</value>
    public ImmutableArray<byte> InlineBytes { get; }

    /// <summary>Gets the content size in bytes, when known.</summary>
    /// <value>Null when unknown; otherwise zero or greater.</value>
    public long? SizeInBytes { get; }

    /// <summary>Gets a content-addressable hash of the referenced bytes, when known.</summary>
    public ContentHash? Hash { get; init; }

    /// <summary>Gets provider-specific or forward-compatible data.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public ExtensionData Extensions
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <inheritdoc/>
    public bool Equals(MediaReference? other) =>
        other is not null
        && Id.Equals(other.Id)
        && SourceKind == other.SourceKind
        && MediaType == other.MediaType
        && string.Equals(Uri?.OriginalString, other.Uri?.OriginalString, StringComparison.Ordinal)
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
        hash.Add(Uri?.OriginalString, StringComparer.Ordinal);
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
