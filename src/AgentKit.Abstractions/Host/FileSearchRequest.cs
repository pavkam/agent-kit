// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one exact, bounded, no-follow workspace content search.</summary>
public sealed record FileSearchRequest
{
    /// <summary>Initializes a content-search request.</summary>
    /// <param name="basePath">The traversal base, or null for the workspace root.</param>
    /// <param name="pattern">The pinned content pattern.</param>
    /// <param name="pathPattern">The simple glob selecting candidate paths relative to the base.</param>
    /// <param name="caseSensitive">Whether content matching is ordinal case-sensitive.</param>
    /// <param name="includeHidden">Whether dot-prefixed names are visited.</param>
    /// <param name="maximumDepth">The positive traversal depth.</param>
    /// <param name="maximumFiles">The positive candidate-file bound.</param>
    /// <param name="maximumBytes">The positive total observed-byte bound.</param>
    /// <param name="maximumMatches">The positive retained-match bound.</param>
    /// <param name="maximumLineBytes">The positive retained line-projection byte bound.</param>
    /// <param name="maximumDuration">The positive monotonic elapsed-time bound.</param>
    /// <param name="grant">The exact authority consumed before traversal.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound is not positive.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public FileSearchRequest(
        FileSystemPath? basePath,
        FileSearchPattern pattern,
        GlobPattern pathPattern,
        bool caseSensitive,
        bool includeHidden,
        int maximumDepth,
        int maximumFiles,
        long maximumBytes,
        int maximumMatches,
        int maximumLineBytes,
        TimeSpan maximumDuration,
        SecurityGrant grant)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFiles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMatches);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLineBytes);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumDuration, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(grant);
        BasePath = basePath;
        Pattern = pattern;
        PathPattern = pathPattern;
        CaseSensitive = caseSensitive;
        IncludeHidden = includeHidden;
        MaximumDepth = maximumDepth;
        MaximumFiles = maximumFiles;
        MaximumBytes = maximumBytes;
        MaximumMatches = maximumMatches;
        MaximumLineBytes = maximumLineBytes;
        MaximumDuration = maximumDuration;
        Grant = grant;
    }

    /// <summary>Gets the traversal base.</summary>
    public FileSystemPath? BasePath { get; init; }
    /// <summary>Gets the content pattern.</summary>
    public FileSearchPattern Pattern { get; init; }
    /// <summary>Gets the candidate-path glob.</summary>
    public GlobPattern PathPattern { get; init; }
    /// <summary>Gets whether content matching is case-sensitive.</summary>
    public bool CaseSensitive { get; init; }
    /// <summary>Gets whether hidden names are visited.</summary>
    public bool IncludeHidden { get; init; }
    /// <summary>Gets the traversal-depth bound.</summary>
    public int MaximumDepth { get; init; }
    /// <summary>Gets the candidate-file bound.</summary>
    public int MaximumFiles { get; init; }
    /// <summary>Gets the total observed-byte bound.</summary>
    public long MaximumBytes { get; init; }
    /// <summary>Gets the retained-match bound.</summary>
    public int MaximumMatches { get; init; }
    /// <summary>Gets the retained line-projection byte bound.</summary>
    public int MaximumLineBytes { get; init; }
    /// <summary>Gets the monotonic elapsed-time bound.</summary>
    public TimeSpan MaximumDuration { get; init; }
    /// <summary>Gets the exact traversal authority.</summary>
    public SecurityGrant Grant { get; init; }
}
