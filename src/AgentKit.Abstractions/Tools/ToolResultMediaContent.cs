// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains a portable media reference without reading or converting its referenced bytes.</summary>
public sealed record ToolResultMediaContent: ToolResultContent
{
    /// <summary>Initializes referenced media terminal content.</summary>
    /// <param name="reference">The immutable media reference.</param>
    /// <param name="extensions">Compatible immutable field evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> or <paramref name="extensions"/> is null, or copied reference text/evidence is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A copied reference identity, source kind, or size is invalid.</exception>
    /// <exception cref="ArgumentException">Copied source fields are uninitialized or inconsistent with their source kind.</exception>
    public ToolResultMediaContent(MediaReference reference, ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentOutOfRangeException.ThrowIfEqual(reference.Id, default, nameof(reference));
        ArgumentOutOfRangeException.ThrowIfUndefined(reference.SourceKind, nameof(reference));
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.MediaType, nameof(reference));
        ArgumentException.ThrowIfDefault(reference.InlineBytes, nameof(reference));
        if (reference.SizeInBytes is { } sizeInBytes)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeInBytes, nameof(reference));
        }
        if (reference.Hash is { } hash)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hash.Value, nameof(reference));
        }
        ArgumentNullException.ThrowIfNull(reference.Extensions, nameof(reference));
        var fieldsMatchSource = reference.SourceKind switch
        {
            MediaSourceKind.InlineBytes => reference.Uri is null,
            MediaSourceKind.Uri => reference.Uri is not null && reference.InlineBytes.IsEmpty,
            MediaSourceKind.FileReference => reference.InlineBytes.IsEmpty,
            _ => false,
        };
        ArgumentException.ThrowIfNotEqual(fieldsMatchSource, true, nameof(reference));
        ArgumentNullException.ThrowIfNull(extensions);
        Reference = reference;
        Extensions = extensions;
    }
    /// <summary>Gets referenced media.</summary><value>A nonnull immutable reference.</value>
    public MediaReference Reference { get; }
    /// <summary>Gets compatible field evidence.</summary><value>A nonnull immutable bag.</value>
    public ExtensionData Extensions { get; }
}
