// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports operation-level and per-entry patch settlement without hiding partial effects.</summary>
public sealed record WorkspacePatchResult
{
    /// <summary>Initializes a patch result.</summary>
    /// <param name="status">The operation-level settlement.</param>
    /// <param name="entries">One source-ordered result for every planned entry.</param>
    /// <param name="safeMessage">A non-sensitive explanation when present.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="entries"/> is default or empty.</exception>
    public WorkspacePatchResult(
        WorkspacePatchStatus status,
        ImmutableArray<WorkspacePatchEntryResult> entries,
        string? safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentException.ThrowIfDefaultOrEmpty(entries);
        ArgumentException.ThrowIfContainsNull(entries);
        Status = status;
        Entries = entries;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the operation-level settlement.</summary>
    public WorkspacePatchStatus Status { get; init; }
    /// <summary>Gets one result for every source entry.</summary>
    public ImmutableArray<WorkspacePatchEntryResult> Entries { get; init; }
    /// <summary>Gets a non-sensitive operation-level explanation when present.</summary>
    public string? SafeMessage { get; init; }

    /// <inheritdoc/>
    public bool Equals(WorkspacePatchResult? other) =>
        other is not null
        && Status == other.Status
        && Entries.SequenceEqual(other.Entries)
        && SafeMessage == other.SafeMessage;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Status);
        foreach (var entry in Entries)
        {
            hash.Add(entry);
        }

        hash.Add(SafeMessage);
        return hash.ToHashCode();
    }
}
