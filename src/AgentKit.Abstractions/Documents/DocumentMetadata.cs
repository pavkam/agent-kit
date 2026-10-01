// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one document's human-facing and source-reference metadata.</summary>
/// <remarks>Large or binary source bytes live in artifact storage; the document stores only the authorized immutable <see cref="SourceArtifact"/> reference. Metadata is untrusted data and never grants authority.</remarks>
public sealed record DocumentMetadata
{
    /// <summary>Initializes validated metadata.</summary>
    /// <param name="title">The non-blank display title.</param>
    /// <param name="mediaType">The non-blank media type of the source.</param>
    /// <param name="sourceArtifact">The immutable artifact reference holding the source bytes, or <see langword="null"/> when the text is the source.</param>
    /// <param name="extensions">Additional extension fields.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException">The title or media type is blank.</exception>
    public DocumentMetadata(string title, string mediaType, ArtifactReference? sourceArtifact, ExtensionData extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentNullException.ThrowIfNull(extensions);
        Title = title;
        MediaType = mediaType;
        SourceArtifact = sourceArtifact;
        Extensions = extensions;
    }

    /// <summary>Gets the display title.</summary>
    public string Title { get; }

    /// <summary>Gets the media type of the source.</summary>
    public string MediaType { get; }

    /// <summary>Gets the immutable artifact reference holding the source bytes, or <see langword="null"/>.</summary>
    public ArtifactReference? SourceArtifact { get; }

    /// <summary>Gets additional extension fields.</summary>
    public ExtensionData Extensions { get; }
}
