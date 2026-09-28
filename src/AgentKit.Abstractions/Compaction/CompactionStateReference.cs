// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One bounded reference to durable state extracted for a summary segment.</summary>
public sealed record CompactionStateReference
{
    /// <summary>Initializes a new instance of the <see cref="CompactionStateReference"/> record.</summary>
    /// <param name="kind">The state category.</param>
    /// <param name="sourceEntryId">The source entry the state was derived from.</param>
    /// <param name="contentHash">A content hash of the referenced state.</param>
    /// <param name="state">Forward-compatible state payload.</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    public CompactionStateReference(
        CompactionStateKind kind,
        SessionEntryId sourceEntryId,
        ContentHash contentHash,
        ExtensionData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Kind = kind;
        SourceEntryId = sourceEntryId;
        ContentHash = contentHash;
        State = state;
    }

    /// <summary>Gets the state category.</summary>
    public CompactionStateKind Kind { get; }

    /// <summary>Gets the source entry the state was derived from.</summary>
    public SessionEntryId SourceEntryId { get; }

    /// <summary>Gets a content hash of the referenced state.</summary>
    public ContentHash ContentHash { get; }

    /// <summary>Gets forward-compatible state payload.</summary>
    public ExtensionData State { get; }
}
