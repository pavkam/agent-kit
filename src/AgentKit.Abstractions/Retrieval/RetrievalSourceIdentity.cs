// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the retrieval source that produced a candidate, with the source version and index watermark it observed.</summary>
/// <remarks>The version and watermark let later stages filter stale hits against authoritative state and let an exposure decision bind exactly what was observed.</remarks>
public sealed record RetrievalSourceIdentity
{
    /// <summary>Initializes a validated identity.</summary>
    /// <param name="key">The source's registration key.</param>
    /// <param name="version">The non-blank source version from its descriptor.</param>
    /// <param name="indexWatermark">The index or store watermark observed when the candidate was produced, or <see langword="null"/> when the source has none.</param>
    /// <exception cref="ArgumentException">The key or version is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="indexWatermark"/> is negative.</exception>
    public RetrievalSourceIdentity(RetrievalSourceKey key, string version, long? indexWatermark)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        if (indexWatermark is { } watermark)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(watermark, nameof(indexWatermark));
        }

        Key = key;
        Version = version;
        IndexWatermark = indexWatermark;
    }

    /// <summary>Gets the source's registration key.</summary>
    public RetrievalSourceKey Key { get; }

    /// <summary>Gets the source version.</summary>
    public string Version { get; }

    /// <summary>Gets the index or store watermark observed, or <see langword="null"/>.</summary>
    public long? IndexWatermark { get; }
}
