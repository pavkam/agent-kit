// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Is the planned outcome of a committed-read request: the entry whose payload to open, or a typed rejection.</summary>
internal sealed record ArtifactReadDecision
{
    private ArtifactReadDecision(ArtifactEntry? entry, ArtifactStoreReadRejected? rejection)
    {
        Entry = entry;
        Rejection = rejection;
    }

    /// <summary>Gets the live committed entry to open, or <see langword="null"/> when the read is rejected.</summary>
    internal ArtifactEntry? Entry { get; }

    /// <summary>Gets the typed rejection, or <see langword="null"/> when the read may proceed.</summary>
    internal ArtifactStoreReadRejected? Rejection { get; }

    /// <summary>Creates a decision that opens an entry's payload.</summary>
    /// <param name="entry">The live committed entry.</param>
    /// <returns>The decision.</returns>
    internal static ArtifactReadDecision Open(ArtifactEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new(entry, null);
    }

    /// <summary>Creates a rejecting decision.</summary>
    /// <param name="kind">The stable failure class.</param>
    /// <param name="message">The non-sensitive explanation.</param>
    /// <returns>The decision.</returns>
    internal static ArtifactReadDecision Reject(ArtifactFailureKind kind, string message) =>
        new(null, new ArtifactStoreReadRejected(new ArtifactFailure(kind, message)));
}
