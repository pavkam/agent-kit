// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies unpublished staged content; it is never a readable artifact reference.</summary>
public sealed record ArtifactPrepared: ArtifactPrepareResult
{
    /// <summary>Initializes a staging receipt.</summary>
    /// <param name="preparationId">The staging identity.</param>
    /// <param name="artifactId">The reserved logical artifact.</param>
    /// <param name="version">The reserved immutable version.</param>
    /// <param name="expiresAt">The staging expiry.</param>
    public ArtifactPrepared(ArtifactPreparationId preparationId, ArtifactId artifactId, ArtifactVersion version, DateTimeOffset expiresAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(artifactId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        PreparationId = preparationId; ArtifactId = artifactId; Version = version; ExpiresAt = expiresAt;
    }
    /// <summary>Gets the staging identity.</summary>
    public ArtifactPreparationId PreparationId { get; }
    /// <summary>Gets the reserved logical artifact.</summary>
    public ArtifactId ArtifactId { get; }
    /// <summary>Gets the reserved immutable version.</summary>
    public ArtifactVersion Version { get; }
    /// <summary>Gets the staging expiry.</summary>
    public DateTimeOffset ExpiresAt { get; }
}
