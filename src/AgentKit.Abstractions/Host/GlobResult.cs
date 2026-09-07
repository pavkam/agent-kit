// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports deterministic glob matches, completeness, observed names, and a typed terminal status.</summary>
public sealed record GlobResult
{
    /// <summary>Initializes a glob result.</summary>
    /// <param name="status">The terminal status.</param>
    /// <param name="matches">The retained ordinal-sorted canonical paths.</param>
    /// <param name="visitedEntries">The number of names observed.</param>
    /// <param name="complete">Whether every eligible entry within the requested depth was visited.</param>
    /// <param name="safeMessage">A non-sensitive explanation for non-success and non-empty outcomes.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined or <paramref name="visitedEntries"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="matches"/> is default.</exception>
    public GlobResult(
        GlobStatus status,
        ImmutableArray<FileSystemPath> matches,
        int visitedEntries,
        bool complete,
        string? safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentException.ThrowIfDefault(matches);
        ArgumentOutOfRangeException.ThrowIfNegative(visitedEntries);
        Status = status;
        Matches = matches;
        VisitedEntries = visitedEntries;
        Complete = complete;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the terminal status.</summary>
    public GlobStatus Status { get; init; }
    /// <summary>Gets the retained ordinal-sorted matches.</summary>
    public ImmutableArray<FileSystemPath> Matches { get; init; }
    /// <summary>Gets the number of names observed.</summary>
    public int VisitedEntries { get; init; }
    /// <summary>Gets whether traversal was complete.</summary>
    public bool Complete { get; init; }
    /// <summary>Gets the non-sensitive explanation, when present.</summary>
    public string? SafeMessage { get; init; }

    /// <inheritdoc/>
    public bool Equals(GlobResult? other) =>
        other is not null
        && Status == other.Status
        && Matches.SequenceEqual(other.Matches)
        && VisitedEntries == other.VisitedEntries
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

        hash.Add(VisitedEntries);
        hash.Add(Complete);
        hash.Add(SafeMessage);
        return hash.ToHashCode();
    }
}
