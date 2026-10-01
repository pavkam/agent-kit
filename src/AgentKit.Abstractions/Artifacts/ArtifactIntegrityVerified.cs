// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that observed content matched its declared length and hash.</summary>
public sealed record ArtifactIntegrityVerified: ArtifactIntegrityResult
{
    /// <summary>Initializes verified integrity.</summary>
    /// <param name="contentHash">The observed complete-content hash.</param>
    /// <exception cref="ArgumentException"><paramref name="contentHash"/> is blank.</exception>
    public ArtifactIntegrityVerified(ContentHash contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash.Value, nameof(contentHash));
        ContentHash = contentHash;
    }

    /// <summary>Gets the observed complete-content hash.</summary>
    public ContentHash ContentHash { get; }
}
