// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports retained matches and completeness for one bounded content search.</summary>
public sealed record FileSearchResult
{
    /// <summary>Initializes a content-search result.</summary>
    /// <param name="status">The typed terminal status.</param>
    /// <param name="matches">The deterministic retained matches.</param>
    /// <param name="visitedFiles">The number of candidate files opened.</param>
    /// <param name="visitedBytes">The number of file bytes observed.</param>
    /// <param name="complete">Whether all eligible files were searched.</param>
    /// <param name="safeMessage">A non-sensitive explanation when present.</param>
    /// <exception cref="ArgumentOutOfRangeException">The status is undefined or a count is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="matches"/> is default.</exception>
    public FileSearchResult(
        FileSearchStatus status,
        ImmutableArray<FileSearchMatch> matches,
        int visitedFiles,
        long visitedBytes,
        bool complete,
        string? safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentException.ThrowIfDefault(matches);
        ArgumentOutOfRangeException.ThrowIfNegative(visitedFiles);
        ArgumentOutOfRangeException.ThrowIfNegative(visitedBytes);
        Status = status;
        Matches = matches;
        VisitedFiles = visitedFiles;
        VisitedBytes = visitedBytes;
        Complete = complete;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the typed terminal status.</summary>
    public FileSearchStatus Status { get; init; }
    /// <summary>Gets the deterministic retained matches.</summary>
    public ImmutableArray<FileSearchMatch> Matches { get; init; }
    /// <summary>Gets the number of candidate files opened.</summary>
    public int VisitedFiles { get; init; }
    /// <summary>Gets the number of file bytes observed.</summary>
    public long VisitedBytes { get; init; }
    /// <summary>Gets whether traversal completed.</summary>
    public bool Complete { get; init; }
    /// <summary>Gets a non-sensitive explanation when present.</summary>
    public string? SafeMessage { get; init; }

    /// <inheritdoc/>
    public bool Equals(FileSearchResult? other) =>
        other is not null
        && Status == other.Status
        && Matches.SequenceEqual(other.Matches)
        && VisitedFiles == other.VisitedFiles
        && VisitedBytes == other.VisitedBytes
        && Complete == other.Complete
        && SafeMessage == other.SafeMessage;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Status);
        foreach (var match in Matches)
        {
            hash.Add(match);
        }

        hash.Add(VisitedFiles);
        hash.Add(VisitedBytes);
        hash.Add(Complete);
        hash.Add(SafeMessage);
        return hash.ToHashCode();
    }
}
