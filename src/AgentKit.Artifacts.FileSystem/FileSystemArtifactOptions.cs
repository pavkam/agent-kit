// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem;

/// <summary>Configures the bounds of a file-system artifact store.</summary>
public sealed class FileSystemArtifactOptions
{
    /// <summary>Gets or sets the largest encoded log record, in bytes.</summary>
    /// <value>A positive bound. The default is one mebibyte.</value>
    public int MaximumRecordBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the largest entry log read back during recovery, in bytes.</summary>
    /// <value>A positive bound. The default is 64 MiB.</value>
    public long MaximumLogBytes { get; set; } = 64L * 1_024 * 1_024;

    /// <summary>Gets or sets the largest payload file read back, in bytes.</summary>
    /// <value>A positive bound. The default is 64 MiB; the coordinator's own limit normally keeps artifacts well below it.</value>
    public long MaximumPayloadBytes { get; set; } = 64L * 1_024 * 1_024;

    /// <summary>Gets or sets the record count above which recovery compacts the log to one snapshot per entry.</summary>
    /// <value>A positive threshold. The default is 4096.</value>
    public int CompactionRecordThreshold { get; set; } = 4_096;

    /// <summary>Gets or sets how long a file-effect authorization request remains valid.</summary>
    /// <value>A positive duration. The default is one minute.</value>
    public TimeSpan EffectAuthorizationLifetime { get; set; } = TimeSpan.FromMinutes(1);
}
