// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem;

/// <summary>Is the validated, immutable snapshot of a file-system artifact store's bounds.</summary>
public sealed record FileSystemArtifactSettings
{
    /// <summary>Initializes validated settings.</summary>
    /// <param name="maximumRecordBytes">The positive largest encoded log record.</param>
    /// <param name="maximumLogBytes">The positive largest entry log read back.</param>
    /// <param name="maximumPayloadBytes">The positive largest payload file read back.</param>
    /// <param name="compactionRecordThreshold">The positive record count that triggers compaction.</param>
    /// <param name="effectAuthorizationLifetime">The positive validity of a file-effect authorization request.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound or duration is not positive.</exception>
    public FileSystemArtifactSettings(int maximumRecordBytes, long maximumLogBytes, long maximumPayloadBytes, int compactionRecordThreshold, TimeSpan effectAuthorizationLifetime)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecordBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLogBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPayloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(compactionRecordThreshold);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(effectAuthorizationLifetime, TimeSpan.Zero);
        MaximumRecordBytes = maximumRecordBytes;
        MaximumLogBytes = maximumLogBytes;
        MaximumPayloadBytes = maximumPayloadBytes;
        CompactionRecordThreshold = compactionRecordThreshold;
        EffectAuthorizationLifetime = effectAuthorizationLifetime;
    }

    /// <summary>Gets the largest encoded log record.</summary>
    public int MaximumRecordBytes { get; }

    /// <summary>Gets the largest entry log read back.</summary>
    public long MaximumLogBytes { get; }

    /// <summary>Gets the largest payload file read back.</summary>
    public long MaximumPayloadBytes { get; }

    /// <summary>Gets the record count that triggers compaction.</summary>
    public int CompactionRecordThreshold { get; }

    /// <summary>Gets the validity of a file-effect authorization request.</summary>
    public TimeSpan EffectAuthorizationLifetime { get; }

    /// <summary>Creates settings with every documented default.</summary>
    /// <returns>Default settings: one-mebibyte records, 64 MiB log and payloads, compaction above 4096 records, and one-minute effect authorizations.</returns>
    public static FileSystemArtifactSettings CreateDefault() => new(1_048_576, 64L * 1_024 * 1_024, 64L * 1_024 * 1_024, 4_096, TimeSpan.FromMinutes(1));
}
