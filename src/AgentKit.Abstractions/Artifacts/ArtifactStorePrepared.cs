// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a store staged unpublished bytes; the receipt is never a readable reference.</summary>
public sealed record ArtifactStorePrepared: ArtifactStorePrepareResult
{
    /// <summary>Initializes a store staging receipt.</summary>
    /// <param name="preparationId">The staging identity.</param>
    /// <param name="artifactId">The reserved logical artifact.</param>
    /// <param name="version">The reserved immutable version.</param>
    /// <param name="expiresAt">The staging expiry.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is empty.</exception>
    /// <exception cref="ArgumentException"><paramref name="version"/> is blank.</exception>
    public ArtifactStorePrepared(ArtifactPreparationId preparationId, ArtifactId artifactId, ArtifactVersion version, DateTimeOffset expiresAt)
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
