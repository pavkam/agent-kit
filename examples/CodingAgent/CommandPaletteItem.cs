// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Describes one searchable application action rendered by the terminal command palette.</summary>
internal sealed record CommandPaletteItem
{
    /// <summary>Creates one immutable searchable application action.</summary>
    /// <param name="id">The stable application-local action identity.</param>
    /// <param name="group">The short category used to scan related actions.</param>
    /// <param name="title">The concise action label.</param>
    /// <param name="description">The outcome shown below the label.</param>
    /// <param name="shortcut">The optional keyboard gesture displayed beside the label.</param>
    /// <param name="keywords">Additional normalized search terms that need not appear in the visible copy.</param>
    /// <param name="badge">Optional current-state evidence displayed with the category.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/>, <paramref name="group"/>, <paramref name="title"/>, or <paramref name="description"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="keywords"/> is null.</exception>
    internal CommandPaletteItem(
        string id,
        string group,
        string title,
        string description,
        string? shortcut = null,
        string keywords = "",
        string? badge = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(keywords);
        Id = id;
        Group = group;
        Title = title;
        Description = description;
        Shortcut = shortcut;
        Keywords = keywords;
        Badge = badge;
    }

    /// <summary>Gets the stable application-local action identity.</summary><value>A nonempty identifier consumed by the screen dispatcher.</value>
    internal string Id { get; }

    /// <summary>Gets the short category used to scan related actions.</summary><value>A nonempty visible category.</value>
    internal string Group { get; }

    /// <summary>Gets the concise action label.</summary><value>A nonempty visible label.</value>
    internal string Title { get; }

    /// <summary>Gets the outcome shown below the label.</summary><value>A nonempty visible description.</value>
    internal string Description { get; }

    /// <summary>Gets the optional keyboard gesture displayed beside the label.</summary><value>The display-only gesture or null.</value>
    internal string? Shortcut { get; }

    /// <summary>Gets additional normalized terms included in search.</summary><value>A nonnull string that may be empty.</value>
    internal string Keywords { get; }

    /// <summary>Gets optional current-state evidence displayed with the category.</summary><value>The badge text or null.</value>
    internal string? Badge { get; }

    /// <summary>Gets the complete case-insensitive text searched by the palette resolver.</summary>
    /// <value>The group, label, description, keywords, shortcut, and badge in deterministic order.</value>
    internal string SearchText => $"{Group} {Title} {Description} {Keywords} {Shortcut} {Badge}";
}
