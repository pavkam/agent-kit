// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records verified content integrity and its verification time.</summary>
public sealed record ArtifactIntegrity
{
    /// <summary>Initializes verified integrity evidence.</summary>
    /// <param name="contentHash">The exact complete-content hash.</param>
    /// <param name="verifiedAt">The verification time.</param>
    public ArtifactIntegrity(ContentHash contentHash, DateTimeOffset verifiedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash.Value, nameof(contentHash));
        ContentHash = contentHash;
        VerifiedAt = verifiedAt;
    }
    /// <summary>Gets the exact complete-content hash.</summary>
    public ContentHash ContentHash { get; }
    /// <summary>Gets when integrity was verified.</summary>
    public DateTimeOffset VerifiedAt { get; }
}
