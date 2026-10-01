// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Json;

/// <summary>Is the validated, immutable snapshot of a JSON goal store's bounds and encoding contract.</summary>
public sealed record JsonGoalStoreSettings
{
    /// <summary>Initializes validated settings.</summary>
    /// <param name="maximumRecordBytes">The positive largest encoded record.</param>
    /// <param name="maximumDocumentBytes">The positive largest manifest document.</param>
    /// <param name="compactionRecordThreshold">The positive record count that triggers compaction.</param>
    /// <param name="authorizedIntentScanners">The scanner identities allowed to discover intents.</param>
    /// <param name="encoding">The non-null encoding contract.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound is not positive.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="authorizedIntentScanners"/> or <paramref name="encoding"/> is null.</exception>
    public JsonGoalStoreSettings(
        int maximumRecordBytes,
        int maximumDocumentBytes,
        int compactionRecordThreshold,
        IEnumerable<ComponentId> authorizedIntentScanners,
        JsonEncodingSettings encoding)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecordBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDocumentBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(compactionRecordThreshold);
        ArgumentNullException.ThrowIfNull(authorizedIntentScanners);
        ArgumentNullException.ThrowIfNull(encoding);
        MaximumRecordBytes = maximumRecordBytes;
        MaximumDocumentBytes = maximumDocumentBytes;
        CompactionRecordThreshold = compactionRecordThreshold;
        AuthorizedIntentScanners = [.. authorizedIntentScanners];
        Encoding = encoding;
    }

    /// <summary>Gets the largest encoded record.</summary>
    public int MaximumRecordBytes { get; }

    /// <summary>Gets the largest manifest document.</summary>
    public int MaximumDocumentBytes { get; }

    /// <summary>Gets the record count that triggers compaction.</summary>
    public int CompactionRecordThreshold { get; }

    /// <summary>Gets the scanner identities allowed to discover intents.</summary>
    public ImmutableArray<ComponentId> AuthorizedIntentScanners { get; }

    /// <summary>Gets the encoding contract.</summary>
    public JsonEncodingSettings Encoding { get; }

    /// <summary>Creates settings with every documented default.</summary>
    /// <returns>Default settings with no authorized scanner.</returns>
    public static JsonGoalStoreSettings CreateDefault() => new(1_048_576, 1_048_576, 4_096, [], JsonEncodingSettings.CreateDefault());
}
