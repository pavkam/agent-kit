// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one workspace-relative source range and the document version observed when available.</summary>
public sealed record LanguageLocation
{
    /// <summary>Initializes one bounded source location.</summary>
    /// <param name="path">The canonical workspace-relative document path.</param>
    /// <param name="range">The half-open source range.</param>
    /// <param name="documentFingerprint">The observed document fingerprint, or null when the provider cannot report one.</param>
    public LanguageLocation(FileSystemPath path, LanguageRange range, ContentHash? documentFingerprint)
    {
        Path = path;
        Range = range;
        DocumentFingerprint = documentFingerprint;
    }

    /// <summary>Gets the canonical workspace-relative document path.</summary>
    public FileSystemPath Path { get; }
    /// <summary>Gets the half-open source range.</summary>
    public LanguageRange Range { get; }
    /// <summary>Gets the observed document fingerprint when known.</summary>
    public ContentHash? DocumentFingerprint { get; }
}
