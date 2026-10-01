// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Json;

/// <summary>Is the validated, immutable snapshot of a JSON artifact store's bounds and encoding contract.</summary>
public sealed record JsonArtifactSettings
{
    /// <summary>Initializes validated settings.</summary>
    /// <param name="maximumRecordBytes">The positive largest encoded log record.</param>
    /// <param name="maximumDocumentBytes">The positive largest manifest document.</param>
    /// <param name="maximumPayloadBytes">The positive largest payload file read back.</param>
    /// <param name="compactionRecordThreshold">The positive record count that triggers compaction.</param>
    /// <param name="encoding">The non-null encoding contract.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound is not positive.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="encoding"/> is null.</exception>
    public JsonArtifactSettings(int maximumRecordBytes, int maximumDocumentBytes, int maximumPayloadBytes, int compactionRecordThreshold, JsonEncodingSettings encoding)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecordBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDocumentBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPayloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(compactionRecordThreshold);
        ArgumentNullException.ThrowIfNull(encoding);
        MaximumRecordBytes = maximumRecordBytes;
        MaximumDocumentBytes = maximumDocumentBytes;
        MaximumPayloadBytes = maximumPayloadBytes;
        CompactionRecordThreshold = compactionRecordThreshold;
        Encoding = encoding;
    }

    /// <summary>Gets the largest encoded log record.</summary>
    public int MaximumRecordBytes { get; }

    /// <summary>Gets the largest manifest document.</summary>
    public int MaximumDocumentBytes { get; }

    /// <summary>Gets the largest payload file read back.</summary>
    public int MaximumPayloadBytes { get; }

    /// <summary>Gets the record count that triggers compaction.</summary>
    public int CompactionRecordThreshold { get; }

    /// <summary>Gets the encoding contract.</summary>
    public JsonEncodingSettings Encoding { get; }

    /// <summary>Creates settings with every documented default.</summary>
    /// <returns>Default settings: one-mebibyte records and manifests, 64 MiB payloads, compaction above 4096 records, and the canonical encoding.</returns>
    public static JsonArtifactSettings CreateDefault() => new(1_048_576, 1_048_576, 64 * 1_024 * 1_024, 4_096, JsonEncodingSettings.CreateDefault());
}
