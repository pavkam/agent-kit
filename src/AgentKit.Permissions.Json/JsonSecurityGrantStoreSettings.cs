// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>Captures immutable positive evidence bounds, compaction policy, and encoding contract for one JSON grant store.</summary>
public sealed record JsonSecurityGrantStoreSettings
{
    /// <summary>Initializes explicit evidence bounds, compaction policy, and the frozen JSON encoding contract.</summary>
    /// <param name="maximumRecordBytes">The positive maximum encoded size of one persisted transition record.</param>
    /// <param name="maximumDocumentBytes">The positive maximum encoded size of the store manifest document.</param>
    /// <param name="compactionRecordThreshold">The positive replayed-record count above which initialization rewrites a log to its live state.</param>
    /// <param name="encoding">The frozen JSON encoding contract bound to the store root.</param>
    /// <exception cref="ArgumentNullException"><paramref name="encoding"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A byte bound or the compaction threshold is not positive.</exception>
    public JsonSecurityGrantStoreSettings(
        int maximumRecordBytes,
        int maximumDocumentBytes,
        int compactionRecordThreshold,
        JsonEncodingSettings encoding)
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

    /// <summary>Gets the maximum encoded transition-record size.</summary>
    /// <value>A positive byte count enforced before an append and while replaying a log.</value>
    public int MaximumRecordBytes { get; }

    /// <summary>Gets the maximum encoded manifest size.</summary>
    /// <value>A positive byte count enforced before the manifest document is decoded.</value>
    public int MaximumDocumentBytes { get; }

    /// <summary>Gets the replayed-record count that triggers compaction.</summary>
    /// <value>
    /// A positive count compared against the number of records replayed at initialization. Exceeding it rewrites the log
    /// from live state so replay cost stays bounded as grants are consumed and revoked over the store's lifetime.
    /// </value>
    public int CompactionRecordThreshold { get; }

    /// <summary>Gets the frozen JSON encoding contract.</summary>
    /// <value>The immutable contract whose fingerprint is bound to the store root at initialization.</value>
    public JsonEncodingSettings Encoding { get; }

    /// <summary>Creates conservative explicit defaults for local applications.</summary>
    /// <returns>Settings with one-mebibyte payload bounds, a 4096-record compaction threshold, and the canonical encoding contract.</returns>
    public static JsonSecurityGrantStoreSettings CreateDefault() => new(
        1_048_576,
        1_048_576,
        4_096,
        JsonEncodingSettings.CreateDefault());
}
