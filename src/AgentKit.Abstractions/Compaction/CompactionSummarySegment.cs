// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One bounded segment handed to a summary generator.</summary>
public sealed record CompactionSummarySegment
{
    /// <summary>Initializes a new instance of the <see cref="CompactionSummarySegment"/> record.</summary>
    /// <param name="sourceEntryIds">The covered source entry identities in order.</param>
    /// <param name="messages">The message projection for the segment.</param>
    /// <param name="state">Extracted state references for the segment.</param>
    /// <param name="contentHash">A hash of the segment payload.</param>
    /// <exception cref="ArgumentException">An array parameter is default or uninitialized.</exception>
    public CompactionSummarySegment(
        ImmutableArray<SessionEntryId> sourceEntryIds,
        ImmutableArray<AgentMessage> messages,
        ImmutableArray<CompactionStateReference> state,
        ContentHash contentHash)
    {
        ArgumentException.ThrowIfDefault(sourceEntryIds);
        ArgumentException.ThrowIfDefault(messages);
        ArgumentException.ThrowIfDefault(state);
        ArgumentOutOfRangeException.ThrowIfEqual(contentHash, default);
        SourceEntryIds = sourceEntryIds;
        Messages = messages;
        State = state;
        ContentHash = contentHash;
    }

    /// <summary>Gets the covered source entry identities in order.</summary>
    public ImmutableArray<SessionEntryId> SourceEntryIds { get; }

    /// <summary>Gets the message projection for the segment.</summary>
    public ImmutableArray<AgentMessage> Messages { get; }

    /// <summary>Gets extracted state references for the segment.</summary>
    public ImmutableArray<CompactionStateReference> State { get; }

    /// <summary>Gets a hash of the segment payload.</summary>
    public ContentHash ContentHash { get; }
}
