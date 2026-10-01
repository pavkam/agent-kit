// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json;

/// <summary>Is the validated, immutable snapshot of a JSON memory store's bounds and encoding contract.</summary>
public sealed record JsonMemorySettings
{
    /// <summary>Initializes validated settings.</summary>
    /// <param name="maximumRecordBytes">The positive largest encoded record.</param>
    /// <param name="maximumDocumentBytes">The positive largest manifest document.</param>
    /// <param name="compactionRecordThreshold">The positive record count that triggers compaction.</param>
    /// <param name="encoding">The non-null encoding contract.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound is not positive.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="encoding"/> is null.</exception>
    public JsonMemorySettings(int maximumRecordBytes, int maximumDocumentBytes, int compactionRecordThreshold, JsonEncodingSettings encoding)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecordBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDocumentBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(compactionRecordThreshold);
        ArgumentNullException.ThrowIfNull(encoding);
        MaximumRecordBytes = maximumRecordBytes;
        MaximumDocumentBytes = maximumDocumentBytes;
        CompactionRecordThreshold = compactionRecordThreshold;
        Encoding = encoding;
    }

    /// <summary>Gets the largest encoded record.</summary>
    public int MaximumRecordBytes { get; }

    /// <summary>Gets the largest manifest document.</summary>
    public int MaximumDocumentBytes { get; }

    /// <summary>Gets the record count that triggers compaction.</summary>
    public int CompactionRecordThreshold { get; }

    /// <summary>Gets the encoding contract.</summary>
    public JsonEncodingSettings Encoding { get; }

    /// <summary>Creates settings with every documented default.</summary>
    /// <returns>Default settings: 16 MiB records, one mebibyte manifests, and compaction above 4096 records.</returns>
    public static JsonMemorySettings CreateDefault() => new(16_777_216, 1_048_576, 4_096, JsonEncodingSettings.CreateDefault());
}
