// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one bounded, deterministic, no-follow workspace glob traversal.</summary>
public sealed record GlobRequest
{
    /// <summary>Initializes a glob request.</summary>
    /// <param name="basePath">The child directory to traverse, or null for the root.</param>
    /// <param name="pattern">The simple glob matched relative to <paramref name="basePath"/>.</param>
    /// <param name="caseSensitive">Whether literal and wildcard matching uses ordinal case sensitivity.</param>
    /// <param name="includeHidden">Whether names beginning with a dot are visited and returned.</param>
    /// <param name="maximumDepth">The positive maximum recursive directory depth.</param>
    /// <param name="maximumVisitedEntries">The positive maximum names observed.</param>
    /// <param name="maximumResults">The positive maximum retained matches.</param>
    /// <param name="grant">The bounded authority consumed before traversal.</param>
    /// <exception cref="ArgumentOutOfRangeException">Any maximum is not positive.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public GlobRequest(
        FileSystemPath? basePath,
        GlobPattern pattern,
        bool caseSensitive,
        bool includeHidden,
        int maximumDepth,
        int maximumVisitedEntries,
        int maximumResults,
        SecurityGrant grant)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumVisitedEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResults);
        ArgumentNullException.ThrowIfNull(grant);
        BasePath = basePath;
        Pattern = pattern;
        CaseSensitive = caseSensitive;
        IncludeHidden = includeHidden;
        MaximumDepth = maximumDepth;
        MaximumVisitedEntries = maximumVisitedEntries;
        MaximumResults = maximumResults;
        Grant = grant;
    }

    /// <summary>Gets the traversal base, or null for root.</summary>
    public FileSystemPath? BasePath { get; init; }
    /// <summary>Gets the pinned simple glob.</summary>
    public GlobPattern Pattern { get; init; }
    /// <summary>Gets whether matching is ordinal case-sensitive.</summary>
    public bool CaseSensitive { get; init; }
    /// <summary>Gets whether dot-prefixed names are visited.</summary>
    public bool IncludeHidden { get; init; }
    /// <summary>Gets the recursive directory depth bound.</summary>
    public int MaximumDepth { get; init; }
    /// <summary>Gets the observed-entry bound.</summary>
    public int MaximumVisitedEntries { get; init; }
    /// <summary>Gets the retained-match bound.</summary>
    public int MaximumResults { get; init; }
    /// <summary>Gets the bounded traversal authority.</summary>
    public SecurityGrant Grant { get; init; }
}
