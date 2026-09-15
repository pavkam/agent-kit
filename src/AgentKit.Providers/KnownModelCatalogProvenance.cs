// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>Records where a <see cref="KnownModelCatalog"/>'s data came from and when it was captured.</summary>
/// <remarks>
/// The architecture requires every descriptor source to state its provenance and freshness rather than
/// present feed data as verified fact. Instances are immutable value objects.
/// </remarks>
public sealed record KnownModelCatalogProvenance
{
    /// <summary>Initializes a new instance of the <see cref="KnownModelCatalogProvenance"/> record.</summary>
    /// <param name="sourceName">A short name for the upstream feed, such as a repository slug.</param>
    /// <param name="sourceUrl">The URL the feed was fetched from.</param>
    /// <param name="sourceCommit">The upstream revision the feed was generated from, when it publishes one.</param>
    /// <param name="generatedAt">When the upstream feed was generated.</param>
    /// <param name="importedAt">When the feed was converted into this catalog's resource.</param>
    /// <exception cref="ArgumentException"><paramref name="sourceName"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="sourceUrl"/> is null.</exception>
    public KnownModelCatalogProvenance(
        string sourceName,
        Uri sourceUrl,
        string? sourceCommit,
        DateTimeOffset generatedAt,
        DateTimeOffset importedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentNullException.ThrowIfNull(sourceUrl);

        SourceName = sourceName;
        SourceUrl = sourceUrl;
        SourceCommit = sourceCommit;
        GeneratedAt = generatedAt;
        ImportedAt = importedAt;
    }

    /// <summary>Gets a short name for the upstream feed.</summary>
    public string SourceName { get; }

    /// <summary>Gets the URL the feed was fetched from.</summary>
    public Uri SourceUrl { get; }

    /// <summary>Gets the upstream revision the feed was generated from, or <see langword="null"/> when unpublished.</summary>
    public string? SourceCommit { get; }

    /// <summary>Gets when the upstream feed was generated.</summary>
    public DateTimeOffset GeneratedAt { get; }

    /// <summary>Gets when the feed was converted into this catalog's resource.</summary>
    public DateTimeOffset ImportedAt { get; }
}
