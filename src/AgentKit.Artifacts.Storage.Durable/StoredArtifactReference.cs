// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Is the persisted form of the committed <see cref="ArtifactReference"/> a finalized entry carries.</summary>
/// <param name="ProfileKey">The captured profile key text.</param>
/// <param name="ProfileVersion">The captured profile revision.</param>
/// <param name="ContentHash">The integrity hash text.</param>
/// <param name="VerifiedAt">The integrity verification instant.</param>
/// <param name="Length">The complete byte length.</param>
/// <param name="CreatedAt">The publication instant.</param>
internal sealed record StoredArtifactReference(
    string ProfileKey,
    long ProfileVersion,
    string ContentHash,
    DateTimeOffset VerifiedAt,
    long Length,
    DateTimeOffset CreatedAt)
{
    /// <summary>Converts a published reference to its persisted form.</summary>
    /// <param name="value">The non-null reference.</param>
    /// <returns>The document.</returns>
    internal static StoredArtifactReference FromDomain(ArtifactReference value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.ProfileKey.Value, value.ProfileVersion.Value, value.Integrity.ContentHash.Value, value.Integrity.VerifiedAt,
            value.Length, value.CreatedAt);
    }
}
