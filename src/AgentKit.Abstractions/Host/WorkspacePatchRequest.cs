// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one preplanned patch batch whose concrete entries are already separately authorized.</summary>
public sealed record WorkspacePatchRequest
{
    /// <summary>Initializes a patch request.</summary>
    /// <param name="entries">The non-empty source-ordered entry plan.</param>
    /// <exception cref="ArgumentException"><paramref name="entries"/> is default, empty, or contains null.</exception>
    public WorkspacePatchRequest(ImmutableArray<WorkspacePatchEntry> entries)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(entries);
        ArgumentException.ThrowIfContainsNull(entries);

        Entries = entries;
    }

    /// <summary>Gets the source-ordered exact entry plan.</summary>
    public ImmutableArray<WorkspacePatchEntry> Entries { get; init; }
}
