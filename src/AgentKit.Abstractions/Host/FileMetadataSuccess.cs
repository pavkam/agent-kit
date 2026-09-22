// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports successful metadata observation for one target.</summary>
public sealed record FileMetadataSuccess: FileMetadataResult
{
    /// <summary>Initializes successful metadata evidence.</summary>
    /// <param name="metadata">The observed metadata snapshot.</param>
    /// <exception cref="ArgumentNullException"><paramref name="metadata"/> is null.</exception>
    public FileMetadataSuccess(FileMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Metadata = metadata;
    }

    /// <summary>Gets the observed metadata snapshot.</summary>
    public FileMetadata Metadata { get; init; }
}
