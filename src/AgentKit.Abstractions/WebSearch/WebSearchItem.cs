// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents one ordered provider-returned web-search candidate.</summary>
public sealed record WebSearchItem
{
    /// <summary>Initializes one search candidate.</summary>
    /// <param name="title">The non-blank result title.</param>
    /// <param name="url">The absolute HTTP(S) result URL without user information.</param>
    /// <param name="snippet">The non-blank untrusted result excerpt.</param>
    /// <param name="publishedAt">The source publication time when reported.</param>
    /// <exception cref="ArgumentException">Text is blank or <paramref name="url"/> is not an acceptable absolute HTTP(S) URL.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> is null.</exception>
    public WebSearchItem(string title, Uri url, string snippet, DateTimeOffset? publishedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNotAbsoluteUri(url);
        ArgumentException.ThrowIfInvalidWebResultUri(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(snippet);
        Title = title;
        Url = url;
        Snippet = snippet;
        PublishedAt = publishedAt;
    }

    /// <summary>Gets the result title.</summary>
    public string Title { get; init; }
    /// <summary>Gets the absolute HTTP(S) result URL.</summary>
    public Uri Url { get; init; }
    /// <summary>Gets the untrusted result excerpt.</summary>
    public string Snippet { get; init; }
    /// <summary>Gets the source publication time when reported.</summary>
    public DateTimeOffset? PublishedAt { get; init; }
}
