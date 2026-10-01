// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a profile runtime could not be activated.</summary>
public sealed record MemoryProfileRuntimeUnavailable: MemoryProfileRuntimeSelectionResult
{
    /// <summary>Initializes an unavailable result.</summary>
    /// <param name="profileKey">The requested profile key.</param>
    /// <param name="profileVersion">The requested profile version.</param>
    /// <param name="failure">The typed reason.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="profileKey"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="profileVersion"/> is not positive.</exception>
    public MemoryProfileRuntimeUnavailable(MemoryProfileKey profileKey, MemoryProfileVersion profileVersion, MemoryProfileRuntimeFailure failure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profileVersion.Value, nameof(profileVersion));
        ArgumentNullException.ThrowIfNull(failure);
        ProfileKey = profileKey;
        ProfileVersion = profileVersion;
        Failure = failure;
    }

    /// <summary>Gets the requested profile key.</summary>
    public MemoryProfileKey ProfileKey { get; }

    /// <summary>Gets the requested profile version.</summary>
    public MemoryProfileVersion ProfileVersion { get; }

    /// <summary>Gets the typed reason.</summary>
    public MemoryProfileRuntimeFailure Failure { get; }
}
