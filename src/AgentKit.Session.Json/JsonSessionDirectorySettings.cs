// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Captures immutable positive bounds, compaction policy, and encoding contract for one JSON session directory.</summary>
public sealed record JsonSessionDirectorySettings
{
    /// <summary>Initializes explicit bounds, compaction policy, and the frozen JSON encoding contract.</summary>
    /// <param name="maximumRecordBytes">The positive maximum encoded size of one persisted routing record.</param>
    /// <param name="maximumDocumentBytes">The positive maximum encoded size of the directory manifest document.</param>
    /// <param name="compactionRecordThreshold">The positive replayed-record count above which initialization rewrites the log to its live state.</param>
    /// <param name="encoding">The frozen JSON encoding contract bound to the directory root.</param>
    /// <exception cref="ArgumentNullException"><paramref name="encoding"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A byte bound or the compaction threshold is not positive.</exception>
    public JsonSessionDirectorySettings(
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

    /// <summary>Gets the maximum encoded routing-record size.</summary>
    /// <value>A positive byte count enforced before an append and while replaying the log.</value>
    public int MaximumRecordBytes { get; }

    /// <summary>Gets the maximum encoded manifest size.</summary>
    /// <value>A positive byte count enforced before the manifest document is decoded.</value>
    public int MaximumDocumentBytes { get; }

    /// <summary>Gets the replayed-record count that triggers compaction.</summary>
    /// <value>A positive count compared against the number of records replayed at initialization.</value>
    public int CompactionRecordThreshold { get; }

    /// <summary>Gets the frozen JSON encoding contract.</summary>
    /// <value>
    /// The immutable host-configured contract. The directory layers its required polymorphism resolver and identity
    /// converters onto it and binds the resulting fingerprint to the root at initialization.
    /// </value>
    public JsonEncodingSettings Encoding { get; }

    /// <summary>Creates conservative explicit defaults for local applications.</summary>
    /// <returns>Settings with one-mebibyte payload bounds, a 4096-record compaction threshold, and the canonical encoding contract.</returns>
    public static JsonSessionDirectorySettings CreateDefault() => new(
        1_048_576,
        1_048_576,
        4_096,
        JsonEncodingSettings.CreateDefault());
}
